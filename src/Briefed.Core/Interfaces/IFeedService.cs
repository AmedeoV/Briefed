using Briefed.Core.Entities;

namespace Briefed.Core.Interfaces;

public interface IFeedService
{
    Task<Feed?> GetFeedByIdAsync(int feedId);
    Task<Feed?> GetFeedByUrlAsync(string url);
    Task<IEnumerable<Feed>> GetAllFeedsAsync();
    Task<IEnumerable<Feed>> GetUserFeedsAsync(string userId);
    Task<Feed> CreateFeedAsync(Feed feed);
    Task UpdateFeedAsync(Feed feed);
    Task DeleteFeedAsync(int feedId);
    Task<bool> SubscribeUserToFeedAsync(string userId, int feedId);
    Task<bool> UnsubscribeUserFromFeedAsync(string userId, int feedId);
}
