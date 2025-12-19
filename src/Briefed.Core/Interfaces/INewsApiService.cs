using Briefed.Core.Models;

namespace Briefed.Core.Interfaces;

public interface INewsApiService
{
    Task<List<TrendingArticle>> GetTrendingArticlesAsync(string category = "general", int count = 50);
}
