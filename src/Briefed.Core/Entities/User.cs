using Microsoft.AspNetCore.Identity;

namespace Briefed.Core.Entities;

public class User : IdentityUser
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public ICollection<UserFeed> UserFeeds { get; set; } = new List<UserFeed>();
    public ICollection<UserArticle> UserArticles { get; set; } = new List<UserArticle>();
    public ICollection<SavedArticle> SavedArticles { get; set; } = new List<SavedArticle>();
}
