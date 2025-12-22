using Briefed.Core.Entities;

namespace Briefed.Core.Interfaces;

public interface ISummaryService
{
    Task<Summary?> GetSummaryByArticleIdAsync(int articleId);
    Task<Summary> GenerateSummaryAsync(int articleId, string articleContent, string summaryType = "comprehensive");
}
