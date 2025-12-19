namespace Briefed.Core.Entities;

public class Article
{
    public int Id { get; set; }
    public int FeedId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Author { get; set; }
    public DateTime PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? ImageUrl { get; set; }
    
    // Navigation properties
    public Feed Feed { get; set; } = null!;
    public Summary? Summary { get; set; }
    public ICollection<UserArticle> UserArticles { get; set; } = new List<UserArticle>();
    public ICollection<SavedArticle> SavedArticles { get; set; } = new List<SavedArticle>();
}
