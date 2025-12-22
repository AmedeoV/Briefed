namespace Briefed.Web.Models;

public class TrendingArticleRequest
{
    public string Url { get; set; } = string.Empty;
    public string? SummaryType { get; set; }
}
