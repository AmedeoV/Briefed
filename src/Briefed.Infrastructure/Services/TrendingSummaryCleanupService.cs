using Briefed.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Briefed.Infrastructure.Services;

public class TrendingSummaryCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TrendingSummaryCleanupService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(6);

    public TrendingSummaryCleanupService(
        IServiceProvider serviceProvider,
        ILogger<TrendingSummaryCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Trending Summary Cleanup Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredSummariesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while cleaning up expired trending summaries");
            }

            await Task.Delay(_cleanupInterval, stoppingToken);
        }

        _logger.LogInformation("Trending Summary Cleanup Service stopped");
    }

    private async Task CleanupExpiredSummariesAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BriefedDbContext>();

        var now = DateTime.UtcNow;
        var expiredSummaries = await context.TrendingSummaries
            .Where(ts => ts.ExpiresAt <= now)
            .ToListAsync();

        if (expiredSummaries.Any())
        {
            context.TrendingSummaries.RemoveRange(expiredSummaries);
            await context.SaveChangesAsync();
            
            _logger.LogInformation("Cleaned up {Count} expired trending summaries", expiredSummaries.Count);
        }
        else
        {
            _logger.LogDebug("No expired trending summaries to clean up");
        }
    }
}
