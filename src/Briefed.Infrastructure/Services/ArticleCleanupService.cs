using Briefed.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Briefed.Infrastructure.Services;

public class ArticleCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ArticleCleanupService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromDays(1); // Check daily
    private readonly int _articleRetentionDays = 30;

    public ArticleCleanupService(
        IServiceProvider serviceProvider,
        ILogger<ArticleCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Article Cleanup Service started - will delete articles older than {Days} days", _articleRetentionDays);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupOldArticlesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while cleaning up old articles");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Article Cleanup Service stopped");
    }

    private async Task CleanupOldArticlesAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BriefedDbContext>();

        var cutoffDate = DateTime.UtcNow.AddDays(-_articleRetentionDays);
        var oldArticles = await context.Articles
            .Where(a => a.PublishedAt < cutoffDate)
            .ToListAsync();

        if (oldArticles.Any())
        {
            context.Articles.RemoveRange(oldArticles);
            await context.SaveChangesAsync();
            
            _logger.LogInformation("Cleaned up {Count} articles older than {Days} days (published before {Date})", 
                oldArticles.Count, _articleRetentionDays, cutoffDate);
        }
        else
        {
            _logger.LogDebug("No old articles to clean up");
        }
    }
}
