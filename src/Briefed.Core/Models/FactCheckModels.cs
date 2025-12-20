namespace Briefed.Core.Models;

public class FactCheckResponse
{
    public string? Claim { get; set; }
    public string? ClaimReview { get; set; }
    public string? Rating { get; set; }
    public string? Publisher { get; set; }
    public string? Url { get; set; }
    public string? TextualRating { get; set; }
    public DateTime? ReviewDate { get; set; }
}

public class FactCheckClaimResult
{
    public string? Text { get; set; }
    public string? Claimant { get; set; }
    public DateTime? ClaimDate { get; set; }
    public List<FactCheckReview> ClaimReview { get; set; } = new();
}

public class FactCheckReview
{
    public string? Publisher { get; set; }
    public string? Url { get; set; }
    public string? Title { get; set; }
    public string? TextualRating { get; set; }
    public string? LanguageCode { get; set; }
    public DateTime? ReviewDate { get; set; }
}
