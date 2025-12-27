namespace Briefed.Core.Entities;

public class ContentReport
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int? ArticleId { get; set; }
    public string ContentType { get; set; } = string.Empty; // "Summary", "FactCheck"
    public string Reason { get; set; } = string.Empty;
    public string? AdditionalDetails { get; set; }
    public DateTime ReportedAt { get; set; } = DateTime.UtcNow;
    public bool IsResolved { get; set; } = false;
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }
    public string? ResolutionNotes { get; set; }
    
    // Navigation properties
    public User User { get; set; } = null!;
    public Article? Article { get; set; }
}
