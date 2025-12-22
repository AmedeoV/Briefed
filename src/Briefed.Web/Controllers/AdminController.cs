using Briefed.Core.Entities;
using Briefed.Core.Interfaces;
using Briefed.Infrastructure.Data;
using Briefed.Infrastructure.Services;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Briefed.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly BriefedDbContext _context;
    private readonly UserManager<User> _userManager;
    private readonly IFeedService _feedService;
    private readonly ILogger<AdminController> _logger;
    private readonly IRecurringJobManager _recurringJobManager;

    public AdminController(
        BriefedDbContext context,
        UserManager<User> userManager,
        IFeedService feedService,
        ILogger<AdminController> logger,
        IRecurringJobManager recurringJobManager)
    {
        _context = context;
        _userManager = userManager;
        _feedService = feedService;
        _logger = logger;
        _recurringJobManager = recurringJobManager;
    }

    public async Task<IActionResult> Index()
    {
        var stats = new AdminDashboardViewModel
        {
            TotalUsers = await _context.Users.CountAsync(),
            TotalFeeds = await _context.Feeds.CountAsync(),
            TotalArticles = await _context.Articles.CountAsync(),
            TotalSummaries = await _context.Summaries.CountAsync(),
            RecentUsers = await _context.Users
                .OrderByDescending(u => u.CreatedAt)
                .Take(10)
                .Select(u => new UserInfo
                {
                    Id = u.Id,
                    Email = u.Email!,
                    CreatedAt = u.CreatedAt,
                    FeedCount = u.UserFeeds.Count
                })
                .ToListAsync(),
            TopFeeds = await _context.Feeds
                .Include(f => f.UserFeeds)
                .OrderByDescending(f => f.UserFeeds.Count)
                .Take(10)
                .Select(f => new FeedInfo
                {
                    Id = f.Id,
                    Title = f.Title,
                    Url = f.Url,
                    SubscriberCount = f.UserFeeds.Count,
                    ArticleCount = f.Articles.Count
                })
                .ToListAsync()
        };

        return View(stats);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult TriggerFeedUpdate()
    {
        try
        {
            BackgroundJob.Enqueue<FeedUpdateService>(service => service.UpdateAllFeedsAsync());
            TempData["Success"] = "Feed update job has been triggered successfully.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trigger feed update");
            TempData["Error"] = "Failed to trigger feed update: " + ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(string id)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction(nameof(Index));
            }

            // Prevent deleting yourself
            var currentUserId = _userManager.GetUserId(User);
            if (user.Id == currentUserId)
            {
                TempData["Error"] = "You cannot delete your own account.";
                return RedirectToAction(nameof(Index));
            }

            await _userManager.DeleteAsync(user);
            TempData["Success"] = $"User {user.Email} has been deleted successfully.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete user {UserId}", id);
            TempData["Error"] = "Failed to delete user: " + ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteFeed(int id)
    {
        try
        {
            var feed = await _context.Feeds.FindAsync(id);
            if (feed == null)
            {
                TempData["Error"] = "Feed not found.";
                return RedirectToAction(nameof(Index));
            }

            _context.Feeds.Remove(feed);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Feed '{feed.Title}' has been deleted successfully.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete feed {FeedId}", id);
            TempData["Error"] = "Failed to delete feed: " + ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CleanupOldArticles(int daysOld = 30)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-daysOld);
            var oldArticles = await _context.Articles
                .Where(a => a.PublishedAt < cutoffDate)
                .ToListAsync();

            _context.Articles.RemoveRange(oldArticles);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Deleted {oldArticles.Count} articles older than {daysOld} days.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup old articles");
            TempData["Error"] = "Failed to cleanup articles: " + ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }
}

public class AdminDashboardViewModel
{
    public int TotalUsers { get; set; }
    public int TotalFeeds { get; set; }
    public int TotalArticles { get; set; }
    public int TotalSummaries { get; set; }
    public List<UserInfo> RecentUsers { get; set; } = new();
    public List<FeedInfo> TopFeeds { get; set; } = new();
}

public class UserInfo
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int FeedCount { get; set; }
}

public class FeedInfo
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public int SubscriberCount { get; set; }
    public int ArticleCount { get; set; }
}
