using Briefed.Core.Entities;
using Briefed.Core.Interfaces;
using Briefed.Core.Models;
using Briefed.Infrastructure.Data;
using Briefed.Infrastructure.Services;
using Briefed.Web.Models;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;

namespace Briefed.Web.Controllers;

[Authorize]
public class ArticlesController : Controller
{
    private readonly IArticleService _articleService;
    private readonly ISummaryService _summaryService;
    private readonly UserManager<User> _userManager;
    private readonly ILogger<ArticlesController> _logger;
    private readonly BriefedDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly IGNewsService _gNewsService;

    public ArticlesController(
        IArticleService articleService,
        ISummaryService summaryService,
        UserManager<User> userManager,
        ILogger<ArticlesController> logger,
        BriefedDbContext context,
        IMemoryCache cache,
        IGNewsService gNewsService)
    {
        _articleService = articleService;
        _summaryService = summaryService;
        _userManager = userManager;
        _logger = logger;
        _context = context;
        _cache = cache;
        _gNewsService = gNewsService;
    }

    public async Task<IActionResult> Index(string filter = "all")
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        
        bool? isRead = filter switch
        {
            "unread" => false,
            "read" => true,
            _ => null
        };

        // Create cache key based on user and filter
        var cacheKey = $"articles_{userId}_{filter}";
        var unreadCacheKey = $"unread_counts_{userId}";
        
        // Try to get articles from cache
        if (!_cache.TryGetValue(cacheKey, out IEnumerable<Article> articles))
        {
            articles = await _articleService.GetUserArticlesAsync(userId, isRead);
            
            // Cache for 5 minutes
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(5))
                .SetSlidingExpiration(TimeSpan.FromMinutes(2));
            
            _cache.Set(cacheKey, articles, cacheOptions);
            _logger.LogInformation("Cached {Count} articles for user {UserId} with filter {Filter}", 
                articles.Count(), userId, filter);
        }
        else
        {
            _logger.LogInformation("Retrieved {Count} articles from cache for user {UserId}", 
                articles.Count(), userId);
        }
        
        // Try to get read article IDs and unread counts from cache
        var readArticleIdsCacheKey = $"read_article_ids_{userId}";
        
        if (!_cache.TryGetValue(readArticleIdsCacheKey, out HashSet<int> readArticleIds))
        {
            readArticleIds = _context.UserArticles
                .Where(ua => ua.UserId == userId && ua.IsRead)
                .Select(ua => ua.ArticleId)
                .ToHashSet();
            
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(5))
                .SetSlidingExpiration(TimeSpan.FromMinutes(2));
            
            _cache.Set(readArticleIdsCacheKey, readArticleIds, cacheOptions);
        }
        
        if (!_cache.TryGetValue(unreadCacheKey, out Dictionary<int, int> unreadCountsByFeed))
        {
            unreadCountsByFeed = articles
                .GroupBy(a => a.FeedId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Count(a => !readArticleIds.Contains(a.Id))
                );
            
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(5))
                .SetSlidingExpiration(TimeSpan.FromMinutes(2));
            
            _cache.Set(unreadCacheKey, unreadCountsByFeed, cacheOptions);
        }
        
        ViewBag.ReadArticleIds = readArticleIds;
        ViewBag.UnreadCountsByFeed = unreadCountsByFeed;
        
        // Get favorite feeds
        var favoriteFeedIds = await _context.UserFeeds
            .Where(uf => uf.UserId == userId && uf.IsFavorite)
            .Select(uf => uf.FeedId)
            .ToListAsync();
        
        ViewBag.FavoriteFeedIds = favoriteFeedIds;
        
        // Log details for debugging
        var feedGroups = articles.GroupBy(a => a.FeedId).ToList();
        _logger.LogInformation("Displaying {TotalArticles} articles from {FeedCount} feeds for user {UserId} with filter {Filter}", 
            articles.Count(), feedGroups.Count, userId, filter);
        
        foreach (var group in feedGroups)
        {
            var feedTitle = group.First().Feed?.Title ?? "Unknown";
            var unreadCount = unreadCountsByFeed.GetValueOrDefault(group.Key, 0);
            _logger.LogInformation("  - {FeedTitle}: {ArticleCount} articles ({UnreadCount} unread)", feedTitle, group.Count(), unreadCount);
        }
        
        ViewData["Filter"] = filter;
        return View(articles);
    }

    public async Task<IActionResult> Read(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var article = await _articleService.GetArticleByIdAsync(id);

        if (article == null)
        {
            return NotFound();
        }

        // Mark as read automatically
        await _articleService.MarkAsReadAsync(userId, id);

        // Fetch full content
        var content = await _articleService.GetArticleContentAsync(article.Url);
        
        // Check if already saved
        var savedArticle = await _articleService.GetSavedArticleAsync(userId, id);

        var viewModel = new ArticleReadViewModel
        {
            Article = article,
            Content = content ?? article.Description ?? "Content not available.",
            IsSaved = savedArticle != null
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Summarise(int id, string summaryType = "comprehensive")
    {
        try
        {
            var article = await _articleService.GetArticleByIdAsync(id);
            if (article == null)
            {
                return NotFound();
            }

            // Check if summary already exists for this type
            var existingSummary = await _summaryService.GetSummaryByArticleIdAsync(id);
            if (existingSummary != null)
            {
                // Return existing summary if available for this type
                var existingContent = summaryType == "concise" 
                    ? existingSummary.ConciseContent 
                    : existingSummary.ComprehensiveContent;
                
                if (!string.IsNullOrEmpty(existingContent))
                {
                    return Json(new { success = true, summary = existingContent });
                }
            }

            // Fetch article content
            var content = await _articleService.GetArticleContentAsync(article.Url);
            if (string.IsNullOrWhiteSpace(content))
            {
                content = article.Description ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                return Json(new { success = false, error = "No content available to summarize." });
            }

            // Generate summary
            var summary = await _summaryService.GenerateSummaryAsync(id, content, summaryType);
            
            // Return the appropriate summary content
            var summaryContent = summaryType == "concise"
                ? summary.ConciseContent
                : summary.ComprehensiveContent;
            
            return Json(new { success = true, summary = summaryContent ?? summary.Content });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate summary for article {ArticleId}", id);
            return Json(new { success = false, error = "Failed to generate summary. Please ensure Ollama is running." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int id)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var article = await _articleService.GetArticleByIdAsync(id);
            
            if (article == null)
            {
                return NotFound();
            }

            // Fetch full content
            var content = await _articleService.GetArticleContentAsync(article.Url);
            if (string.IsNullOrWhiteSpace(content))
            {
                content = article.Description ?? "Content not available";
            }

            await _articleService.SaveArticleAsync(userId, id, content);
            
            TempData["Success"] = "Article saved for offline reading!";
            return RedirectToAction(nameof(Read), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save article {ArticleId}", id);
            TempData["Error"] = "Failed to save article.";
            return RedirectToAction(nameof(Read), new { id });
        }
    }

    public async Task<IActionResult> Saved()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var savedArticles = await _articleService.GetSavedArticlesAsync(userId);
        return View(savedArticles);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSaved(int id)
    {
        await _articleService.DeleteSavedArticleAsync(id);
        TempData["Success"] = "Saved article removed.";
        return RedirectToAction(nameof(Saved));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            await _articleService.MarkAsReadAsync(userId, id);
            
            // Clear cache to ensure counts are refreshed
            _cache.Remove($"articles_{userId}_all");
            _cache.Remove($"articles_{userId}_unread");
            _cache.Remove($"articles_{userId}_read");
            _cache.Remove($"unread_counts_{userId}");
            _cache.Remove($"read_article_ids_{userId}");
            
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark article {ArticleId} as read", id);
            return Json(new { success = false, error = "Failed to mark article as read." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult RefreshFeeds()
    {
        // Clear cache when refreshing feeds
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        _cache.Remove($"articles_{userId}_all");
        _cache.Remove($"articles_{userId}_unread");
        _cache.Remove($"articles_{userId}_read");
        _cache.Remove($"unread_counts_{userId}");
        _cache.Remove($"read_article_ids_{userId}");
        
        BackgroundJob.Enqueue<FeedUpdateService>(service => service.UpdateAllFeedsAsync());
        BackgroundJob.Enqueue<FeedUpdateService>(service => service.UpdateFeedFaviconsAsync());
        TempData["Success"] = "Feed refresh started! New articles will appear shortly.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> SummariseTrendingArticle([FromBody] TrendingArticleRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Url))
            {
                return Json(new { success = false, error = "Article URL is required." });
            }

            // Fetch article content from URL
            var content = await _articleService.GetArticleContentAsync(request.Url);
            
            if (string.IsNullOrWhiteSpace(content))
            {
                return Json(new { success = false, error = "Could not fetch article content." });
            }

            // Generate summary using the summary service (without saving to database)
            var summaryType = request.SummaryType ?? "comprehensive";
            var summary = await _summaryService.GenerateSummaryAsync(0, content, summaryType); // Pass 0 since there's no article ID
            
            // Get the appropriate summary content based on type
            var summaryContent = summaryType == "concise"
                ? summary.ConciseContent
                : summary.ComprehensiveContent;
            
            return Json(new { success = true, summary = summaryContent ?? summary.Content });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate summary for trending article {Url}", request.Url);
            return Json(new { success = false, error = "Failed to generate summary. Please ensure Ollama is running." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Trending(string? category = null, string? country = null, bool refresh = false)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login", "Account");
        }

        // Create cache key based on country and category
        var cacheKey = $"trending_{country ?? "worldwide"}_{category ?? "general"}";
        var lastFetchKey = $"trending_last_fetch_{country ?? "worldwide"}_{category ?? "general"}";
        
        List<TrendingArticle> trendingArticles = new();
        
        // Check if admin wants to force refresh
        var user = await _userManager.GetUserAsync(User);
        var isAdmin = user != null && await _userManager.IsInRoleAsync(user, "Admin");
        
        if (refresh && !isAdmin)
        {
            // Only admins can force refresh
            return RedirectToAction("Trending", new { category, country });
        }
        
        // Try to get from cache (24 hours)
        if (!refresh && _cache.TryGetValue(cacheKey, out trendingArticles!))
        {
            _logger.LogInformation("Retrieved {Count} trending articles from cache for {Country}/{Category}", 
                trendingArticles.Count, country ?? "worldwide", category ?? "general");
        }
        else
        {
            trendingArticles = await _gNewsService.GetTrendingArticlesAsync(country, category, 10);
            
            // Cache for 24 hours to minimize API calls
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromHours(24))
                .SetSlidingExpiration(TimeSpan.FromHours(12));
            
            _cache.Set(cacheKey, trendingArticles, cacheOptions);
            _cache.Set(lastFetchKey, DateTime.UtcNow, cacheOptions);
            
            _logger.LogInformation("Fetched and cached {Count} trending articles for {Country}/{Category} (24h cache)", 
                trendingArticles.Count, country ?? "worldwide", category ?? "general");
        }
        
        // Get last fetch time for display
        _cache.TryGetValue(lastFetchKey, out DateTime lastFetch);
        ViewBag.LastFetch = lastFetch;
        ViewBag.IsAdmin = isAdmin;
        
        ViewBag.Category = category;
        ViewBag.Country = country;
        ViewBag.Categories = new[] { "general", "world", "nation", "business", "technology", "entertainment", "sports", "science", "health" };
        ViewBag.Countries = new Dictionary<string, string>
        {
            { "", "Worldwide" },
            { "us", "United States" },
            { "gb", "United Kingdom" },
            { "au", "Australia" },
            { "ca", "Canada" },
            { "ie", "Ireland" },
            { "it", "Italy" },
            { "in", "India" },
            { "de", "Germany" },
            { "fr", "France" },
            { "jp", "Japan" },
            { "cn", "China" },
            { "br", "Brazil" }
        };
        
        ViewBag.CanRefresh = isAdmin;
        
        return View(trendingArticles);
    }
}
