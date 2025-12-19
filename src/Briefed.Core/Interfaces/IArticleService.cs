using Briefed.Core.Entities;

namespace Briefed.Core.Interfaces;

public interface IArticleService
{
    Task<Article?> GetArticleByIdAsync(int articleId);
    Task<IEnumerable<Article>> GetArticlesByFeedIdAsync(int feedId);
    Task<IEnumerable<Article>> GetUserArticlesAsync(string userId, bool? isRead = null);
    Task<Article> CreateArticleAsync(Article article);
    Task UpdateArticleAsync(Article article);
    Task MarkAsReadAsync(string userId, int articleId);
    Task MarkAllAsReadForFeedAsync(string userId, int feedId);
    Task<string?> GetArticleContentAsync(string url);
    Task<SavedArticle?> SaveArticleAsync(string userId, int articleId, string content);
    Task<IEnumerable<SavedArticle>> GetSavedArticlesAsync(string userId);
    Task<SavedArticle?> GetSavedArticleAsync(string userId, int articleId);
    Task DeleteSavedArticleAsync(int savedArticleId);
}
