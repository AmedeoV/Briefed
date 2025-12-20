namespace Briefed.Web.Models;

public class CheckClaimRequest
{
    public string Claim { get; set; } = string.Empty;
    public string? LanguageCode { get; set; }
}

public class CheckArticleRequest
{
    public string ArticleText { get; set; } = string.Empty;
}
