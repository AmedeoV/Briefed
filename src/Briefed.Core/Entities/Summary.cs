namespace Briefed.Core.Entities;

public class Summary
{
    public int Id { get; set; }
    public int ArticleId { get; set; }
    
    [Obsolete("Use ConciseContent or ComprehensiveContent instead")]
    public string Content { get; set; } = string.Empty;
    
    public string? ConciseContent { get; set; }
    public string? ComprehensiveContent { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Model { get; set; } = string.Empty;
    
    // Navigation property
    public Article Article { get; set; } = null!;
}
