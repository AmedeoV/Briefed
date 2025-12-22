using Briefed.Core.Models;

namespace Briefed.Core.Interfaces;

public interface IGNewsService
{
    Task<List<TrendingArticle>> GetTrendingArticlesAsync(string? country = null, string? category = null, int count = 10);
}
