namespace Briefed.Core.Entities;

public class UserFeed
{
    public string UserId { get; set; } = string.Empty;
    public int FeedId { get; set; }
    public DateTime SubscribedAt { get; set; } = DateTime.UtcNow;
    public bool IsFavorite { get; set; } = false;
    
    // Navigation properties
    public User User { get; set; } = null!;
    public Feed Feed { get; set; } = null!;
}
