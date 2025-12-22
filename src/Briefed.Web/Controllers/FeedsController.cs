using Briefed.Core.Entities;
using Briefed.Core.Interfaces;
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
public class FeedsController : Controller
{
    private readonly IFeedService _feedService;
    private readonly IArticleService _articleService;
    private readonly UserManager<User> _userManager;
    private readonly ILogger<FeedsController> _logger;
    private readonly OpmlImportService _opmlImportService;
    private readonly BriefedDbContext _context;
    private readonly FeedUpdateService _feedUpdateService;
    private readonly IMemoryCache _cache;

    public FeedsController(IFeedService feedService, IArticleService articleService, UserManager<User> userManager, ILogger<FeedsController> logger, OpmlImportService opmlImportService, BriefedDbContext context, FeedUpdateService feedUpdateService, IMemoryCache cache)
    {
        _feedService = feedService;
        _articleService = articleService;
        _userManager = userManager;
        _logger = logger;
        _opmlImportService = opmlImportService;
        _context = context;
        _feedUpdateService = feedUpdateService;
        _cache = cache;
    }

    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var feeds = await _feedService.GetUserFeedsAsync(userId);
        
        // Get unread counts for each feed
        var feedStats = new List<FeedWithStatsViewModel>();
        foreach (var feed in feeds)
        {
            var totalArticles = await _context.Articles.CountAsync(a => a.FeedId == feed.Id);
            
            // Count unread articles (articles that don't have a UserArticle entry or have IsRead = false)
            var readArticleIds = await _context.UserArticles
                .Where(ua => ua.UserId == userId && ua.IsRead)
                .Select(ua => ua.ArticleId)
                .ToListAsync();
            
            var unreadArticles = await _context.Articles
                .Where(a => a.FeedId == feed.Id && !readArticleIds.Contains(a.Id))
                .CountAsync();
            
            feedStats.Add(new FeedWithStatsViewModel
            {
                Feed = feed,
                TotalArticles = totalArticles,
                UnreadArticles = unreadArticles
            });
        }
        
        return View(feedStats);
    }

    public IActionResult Create()
    {
        return View();
    }
    
    public IActionResult Browse(string search = "")
    {
        var directory = GetFeedDirectory();
        
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            directory = directory.Where(f => 
                f.Title.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
                f.Description.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
                f.Category.Contains(searchLower, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }
        
        ViewBag.SearchQuery = search;
        return View(directory);
    }
    
    private List<FeedDirectoryItem> GetFeedDirectory()
    {
        return new List<FeedDirectoryItem>
        {
            // News
            new() { Title = "BBC News", Url = "https://feeds.bbci.co.uk/news/rss.xml", Category = "News", Description = "Latest news from BBC" },
            new() { Title = "BBC Technology", Url = "https://feeds.bbci.co.uk/news/technology/rss.xml", Category = "News", Description = "BBC technology news" },
            new() { Title = "CNN Top Stories", Url = "http://rss.cnn.com/rss/cnn_topstories.rss", Category = "News", Description = "CNN's top news stories" },
            new() { Title = "The Guardian", Url = "https://www.theguardian.com/world/rss", Category = "News", Description = "World news from The Guardian" },
            new() { Title = "Reuters Top News", Url = "https://www.reutersagency.com/feed/?taxonomy=best-topics&post_type=best", Category = "News", Description = "Reuters breaking news" },
            new() { Title = "NPR News", Url = "https://feeds.npr.org/1001/rss.xml", Category = "News", Description = "NPR top news stories" },
            new() { Title = "Al Jazeera", Url = "https://www.aljazeera.com/xml/rss/all.xml", Category = "News", Description = "International news from Al Jazeera" },
            new() { Title = "Associated Press", Url = "https://apnews.com/rss", Category = "News", Description = "AP breaking news" },
            
            // Technology
            new() { Title = "Hacker News", Url = "https://hnrss.org/frontpage", Category = "Technology", Description = "Tech news from Hacker News community" },
            new() { Title = "TechCrunch", Url = "https://techcrunch.com/feed/", Category = "Technology", Description = "Latest technology news and startups" },
            new() { Title = "Ars Technica", Url = "https://feeds.arstechnica.com/arstechnica/index", Category = "Technology", Description = "Tech news and analysis" },
            new() { Title = "The Verge", Url = "https://www.theverge.com/rss/index.xml", Category = "Technology", Description = "Technology, science, and culture" },
            new() { Title = "Wired", Url = "https://www.wired.com/feed/rss", Category = "Technology", Description = "Technology and innovation news" },
            new() { Title = "Engadget", Url = "https://www.engadget.com/rss.xml", Category = "Technology", Description = "Consumer electronics and gadgets" },
            new() { Title = "MIT Technology Review", Url = "https://www.technologyreview.com/feed/", Category = "Technology", Description = "Emerging technology analysis" },
            new() { Title = "ZDNet", Url = "https://www.zdnet.com/news/rss.xml", Category = "Technology", Description = "Business technology news" },
            new() { Title = "Gizmodo", Url = "https://gizmodo.com/rss", Category = "Technology", Description = "Tech and science news" },
            
            // Programming/Dev
            new() { Title = "GitHub Blog", Url = "https://github.blog/feed/", Category = "Programming", Description = "Updates from GitHub" },
            new() { Title = "Dev.to", Url = "https://dev.to/feed", Category = "Programming", Description = "Developer community articles" },
            new() { Title = "CSS-Tricks", Url = "https://css-tricks.com/feed/", Category = "Programming", Description = "Web development tips and tricks" },
            new() { Title = "Smashing Magazine", Url = "https://www.smashingmagazine.com/feed/", Category = "Programming", Description = "Web design and development" },
            new() { Title = "A List Apart", Url = "https://alistapart.com/main/feed/", Category = "Programming", Description = "Web standards and best practices" },
            new() { Title = "Scott Hanselman", Url = "https://www.hanselman.com/blog/feed/rss", Category = "Programming", Description = "Software development insights" },
            new() { Title = "Martin Fowler", Url = "https://martinfowler.com/feed.atom", Category = "Programming", Description = "Software architecture and design" },
            
            // Security
            new() { Title = "Krebs on Security", Url = "https://krebsonsecurity.com/feed/", Category = "Security", Description = "Cybersecurity news and investigation" },
            new() { Title = "Schneier on Security", Url = "https://www.schneier.com/feed/atom/", Category = "Security", Description = "Security and privacy commentary" },
            new() { Title = "The Hacker News", Url = "https://feeds.feedburner.com/TheHackersNews", Category = "Security", Description = "Cybersecurity news" },
            new() { Title = "Threatpost", Url = "https://threatpost.com/feed/", Category = "Security", Description = "Information security news" },
            new() { Title = "Malwarebytes Labs", Url = "https://blog.malwarebytes.org/feed/", Category = "Security", Description = "Malware and security research" },
            
            // Science
            new() { Title = "Scientific American", Url = "https://www.scientificamerican.com/feed/", Category = "Science", Description = "Science news and analysis" },
            new() { Title = "NASA", Url = "https://www.nasa.gov/rss/dyn/breaking_news.rss", Category = "Science", Description = "NASA breaking news" },
            new() { Title = "Nature", Url = "https://www.nature.com/nature.rss", Category = "Science", Description = "Nature journal articles" },
            new() { Title = "Science Daily", Url = "https://www.sciencedaily.com/rss/all.xml", Category = "Science", Description = "Latest science news" },
            new() { Title = "Phys.org", Url = "https://phys.org/rss-feed/", Category = "Science", Description = "Science and technology news" },
            
            // Business/Finance
            new() { Title = "Financial Times", Url = "https://www.ft.com/?format=rss", Category = "Business", Description = "Global business news" },
            new() { Title = "Bloomberg", Url = "https://www.bloomberg.com/feed/podcast/businessweek.xml", Category = "Business", Description = "Business and financial news" },
            new() { Title = "Forbes", Url = "https://www.forbes.com/real-time/feed2/", Category = "Business", Description = "Business news and insights" },
            new() { Title = "Entrepreneur", Url = "https://www.entrepreneur.com/latest.rss", Category = "Business", Description = "Entrepreneurship and startups" },
            
            // Entertainment
            new() { Title = "Variety", Url = "https://variety.com/feed/", Category = "Entertainment", Description = "Entertainment industry news" },
            new() { Title = "IGN", Url = "https://feeds.ign.com/ign/all", Category = "Entertainment", Description = "Gaming and entertainment" },
            new() { Title = "Polygon", Url = "https://www.polygon.com/rss/index.xml", Category = "Entertainment", Description = "Gaming news and culture" },
            new() { Title = "Rolling Stone", Url = "https://www.rollingstone.com/feed/", Category = "Entertainment", Description = "Music and culture news" },
            
            // Design
            new() { Title = "Dribble", Url = "https://dribbble.com/shots/popular.rss", Category = "Design", Description = "Design inspiration" },
            new() { Title = "Awwwards", Url = "https://feeds.feedburner.com/awwwards-sites-of-the-day", Category = "Design", Description = "Web design awards" },
            new() { Title = "Behance", Url = "https://www.behance.net/feeds/projects", Category = "Design", Description = "Creative portfolio showcase" },
        };
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateFeedViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var feed = new Feed
            {
                Url = model.Url,
                Title = model.Title ?? "New Feed"
            };

            var createdFeed = await _feedService.CreateFeedAsync(feed);
            
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            await _feedService.SubscribeUserToFeedAsync(userId, createdFeed.Id);

            TempData["Success"] = "Feed added successfully!";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create feed");
            ModelState.AddModelError("", "Failed to add feed. Please check the URL and try again.");
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unsubscribe(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _feedService.UnsubscribeUserFromFeedAsync(userId, id);
        
        TempData["Success"] = "Unsubscribed from feed.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Import()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(IFormFile opmlFile)
    {
        if (opmlFile == null || opmlFile.Length == 0)
        {
            ModelState.AddModelError("", "Please select an OPML file to import.");
            return View();
        }

        if (!opmlFile.FileName.EndsWith(".opml", StringComparison.OrdinalIgnoreCase) && 
            !opmlFile.FileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError("", "Please upload a valid OPML file (.opml or .xml).");
            return View();
        }

        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            List<(string Title, string FeedUrl)> feeds;

            using (var stream = opmlFile.OpenReadStream())
            {
                feeds = _opmlImportService.ParseOpml(stream);
            }

            if (feeds.Count == 0)
            {
                ModelState.AddModelError("", "No feeds found in the OPML file.");
                return View();
            }

            var importedCount = 0;
            var skippedCount = 0;
            var erroredFeeds = new List<(string Title, string Error)>();
            var totalFeeds = feeds.Count;
            var processedCount = 0;

            foreach (var (title, feedUrl) in feeds)
            {
                processedCount++;
                var progress = (int)((processedCount / (double)totalFeeds) * 100);
                
                _logger.LogInformation("Processing feed {Current}/{Total} ({Progress}%): {Title} - {Url}", 
                    processedCount, totalFeeds, progress, title, feedUrl);
                
                try
                {
                    // Check if user already subscribed to this feed
                    var existingFeed = await _feedService.GetFeedByUrlAsync(feedUrl);
                    if (existingFeed != null)
                    {
                        var userFeeds = await _feedService.GetUserFeedsAsync(userId);
                        if (userFeeds.Any(f => f.Id == existingFeed.Id))
                        {
                            _logger.LogInformation("Skipping already subscribed feed: {Title}", title);
                            skippedCount++;
                            continue;
                        }
                        
                        await _feedService.SubscribeUserToFeedAsync(userId, existingFeed.Id);
                        _logger.LogInformation("Subscribed to existing feed: {Title}", title);
                        importedCount++;
                    }
                    else
                    {
                        var newFeed = new Feed
                        {
                            Url = feedUrl,
                            Title = title
                        };
                        _logger.LogInformation("Creating new feed: {Title} from {Url}", title, feedUrl);
                        var createdFeed = await _feedService.CreateFeedAsync(newFeed);
                        await _feedService.SubscribeUserToFeedAsync(userId, createdFeed.Id);
                        _logger.LogInformation("Successfully imported new feed: {Title}", title);
                        importedCount++;
                    }
                }
                catch (Exception ex)
                {
                    var errorMessage = ex.InnerException?.Message ?? ex.Message;
                    _logger.LogError(ex, "Failed to import feed: {Title} from {Url}. Error: {Error}", 
                        title, feedUrl, errorMessage);
                    erroredFeeds.Add((title, errorMessage));
                }
            }

            var resultMessage = $"Successfully imported {importedCount} feed(s).";
            if (skippedCount > 0)
            {
                resultMessage += $" Skipped {skippedCount} duplicate(s).";
            }
            TempData["Success"] = resultMessage;
            
            if (erroredFeeds.Any())
            {
                var errorSummary = $"Failed to import {erroredFeeds.Count} feed(s). Common issues:\n" +
                    "• 404 errors mean the feed has moved - search the website for the new feed URL\n" +
                    "• SSL/TLS errors may require the feed owner to update their server\n" +
                    "• Timeout errors can be temporary - try importing again later\n\n" +
                    "Details:\n";
                var errorDetails = string.Join("\n", erroredFeeds.Select(e => $"• {e.Title}: {e.Error}"));
                TempData["Error"] = errorSummary + errorDetails;
                _logger.LogWarning("Import completed with errors. Failed feeds: {FailedFeeds}", 
                    string.Join(", ", erroredFeeds.Select(e => e.Title)));
            }

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", ex.Message);
            return View();
        }
    }
    
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> DiagnoseFeed(int id)
    {
        try
        {
            var feed = await _context.Feeds
                .Include(f => f.Articles)
                .FirstOrDefaultAsync(f => f.Id == id);
                
            if (feed == null)
            {
                return Json(new { success = false, error = "Feed not found" });
            }
            
            // Try to fetch the feed
            string fetchError = null;
            int newArticlesCount = 0;
            try
            {
                _logger.LogInformation("Testing feed fetch for: {FeedTitle} ({FeedUrl})", feed.Title, feed.Url);
                await _feedUpdateService.UpdateFeedAsync(feed);
                
                // Reload to get updated article count
                await _context.Entry(feed).Collection(f => f.Articles).LoadAsync();
                newArticlesCount = feed.Articles.Count;
            }
            catch (Exception ex)
            {
                fetchError = $"{ex.GetType().Name}: {ex.Message}";
                _logger.LogError(ex, "Feed diagnostic test failed for {FeedUrl}", feed.Url);
            }
            
            return Json(new 
            { 
                success = fetchError == null,
                feedId = id,
                feedTitle = feed.Title,
                feedUrl = feed.Url,
                lastFetched = feed.LastFetchedAt?.ToString("yyyy-MM-dd HH:mm:ss"),
                totalArticles = feed.Articles.Count,
                fetchError = fetchError,
                recentArticles = feed.Articles
                    .OrderByDescending(a => a.PublishedAt)
                    .Take(5)
                    .Select(a => new { a.Title, a.Url, PublishedAt = a.PublishedAt.ToString("yyyy-MM-dd HH:mm:ss") })
                    .ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DiagnoseFeed for feed {FeedId}", id);
            return Json(new { success = false, error = ex.Message });
        }
    }
    
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RefreshAllArticles()
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            
            _logger.LogInformation("🔄 User {UserId} starting feed refresh", userId);
            
            // Call directly for now since Hangfire isn't working as expected
            await _feedUpdateService.UpdateAllFeedsAsync();
            
            _logger.LogInformation("✓ Feed refresh completed");
            
            TempData["Success"] = "All feeds have been refreshed successfully!";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "✗ Failed to refresh feeds");
            TempData["Error"] = $"Failed to refresh feeds: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }
    
    [HttpGet]
    public async Task<IActionResult> RefreshAllArticlesNow()
    {
        return await RefreshAllArticles();
    }
    
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllAsRead(int id)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var feed = await _context.Feeds.FindAsync(id);
            
            if (feed == null)
            {
                return NotFound();
            }
            
            await _articleService.MarkAllAsReadForFeedAsync(userId, id);
            
            TempData["Success"] = $"All articles from {feed.Title} marked as read.";
            
            // Check if we came from the Articles page
            var referer = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(referer) && referer.Contains("/Articles"))
            {
                return Redirect("/Articles");
            }
            
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark all articles as read for feed {FeedId}", id);
            TempData["Error"] = "Failed to mark articles as read.";
            
            var referer = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(referer) && referer.Contains("/Articles"))
            {
                return Redirect("/Articles");
            }
            
            return RedirectToAction(nameof(Index));
        }
    }
    
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllAsReadJson(int id)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var feed = await _context.Feeds.FindAsync(id);
            
            if (feed == null)
            {
                return Json(new { success = false, message = "Feed not found" });
            }
            
            await _articleService.MarkAllAsReadForFeedAsync(userId, id);
            
            // Clear cache to ensure counts are refreshed
            _cache.Remove($"articles_{userId}_all");
            _cache.Remove($"articles_{userId}_unread");
            _cache.Remove($"articles_{userId}_read");
            _cache.Remove($"unread_counts_{userId}");
            
            return Json(new { success = true, message = $"All articles from {feed.Title} marked as read." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark all articles as read for feed {FeedId}", id);
            return Json(new { success = false, message = "Failed to mark articles as read." });
        }
    }
    
    [HttpPost]
    public async Task<IActionResult> ToggleFavorite(int id)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var userFeed = await _context.UserFeeds
                .FirstOrDefaultAsync(uf => uf.UserId == userId && uf.FeedId == id);
            
            if (userFeed == null)
            {
                return Json(new { success = false, message = "Feed not found" });
            }
            
            userFeed.IsFavorite = !userFeed.IsFavorite;
            await _context.SaveChangesAsync();
            
            return Json(new { success = true, isFavorite = userFeed.IsFavorite });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle favorite for feed {FeedId}", id);
            return Json(new { success = false, message = "Failed to toggle favorite." });
        }
    }
    
    [HttpPost]
    public async Task<IActionResult> ResyncDates(int id)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            
            await _feedUpdateService.ResyncFeedArticleDatesAsync(id);
            
            // Clear cache to show updated dates
            _cache.Remove($"articles_{userId}_all");
            _cache.Remove($"articles_{userId}_unread");
            _cache.Remove($"articles_{userId}_read");
            _cache.Remove($"unread_counts_{userId}");
            
            return Json(new { success = true, message = "Article dates re-synced successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to re-sync dates for feed {FeedId}", id);
            return Json(new { success = false, message = "Failed to re-sync dates: " + ex.Message });
        }
    }
}