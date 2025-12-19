namespace Briefed.Core.Entities;

public class SavedArticle
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int ArticleId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime SavedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public User User { get; set; } = null!;
    public Article Article { get; set; } = null!;
}
