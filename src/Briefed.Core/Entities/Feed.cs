namespace Briefed.Core.Entities;

public class Feed
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SiteUrl { get; set; }
    public string? FaviconUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastFetchedAt { get; set; }
    public bool IsActive { get; set; } = true;
    
    // Navigation properties
    public ICollection<Article> Articles { get; set; } = new List<Article>();
    public ICollection<UserFeed> UserFeeds { get; set; } = new List<UserFeed>();
}
