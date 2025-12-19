namespace Briefed.Core.Models;

public class TrendingArticle
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; }
    public string? ImageUrl { get; set; }
    public string Category { get; set; } = string.Empty;
}
