using Briefed.Core.Models;

namespace Briefed.Core.Interfaces;

public interface IFactCheckService
{
    Task<FactCheckResponse> CheckClaimAsync(string claim, string? languageCode = null);
    Task<List<FactCheckResponse>> CheckArticleAsync(string articleText);
    Task<bool> IsAvailableAsync();
}
