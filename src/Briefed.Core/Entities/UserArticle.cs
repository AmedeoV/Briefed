namespace Briefed.Core.Entities;

public class UserArticle
{
    public string UserId { get; set; } = string.Empty;
    public int ArticleId { get; set; }
    public bool IsRead { get; set; } = false;
    public DateTime? ReadAt { get; set; }
    
    // Navigation properties
    public User User { get; set; } = null!;
    public Article Article { get; set; } = null!;
}
