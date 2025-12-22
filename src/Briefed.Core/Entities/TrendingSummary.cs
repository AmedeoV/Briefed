namespace Briefed.Core.Entities;

public class TrendingSummary
{
    public int Id { get; set; }
    
    /// <summary>
    /// SHA256 hash of the article URL for unique identification
    /// </summary>
    public string UrlHash { get; set; } = string.Empty;
    
    /// <summary>
    /// Original URL for reference
    /// </summary>
    public string Url { get; set; } = string.Empty;
    
    /// <summary>
    /// Article title for reference
    /// </summary>
    public string Title { get; set; } = string.Empty;
    
    public string? ConciseContent { get; set; }
    public string? ComprehensiveContent { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public string Model { get; set; } = string.Empty;
}
