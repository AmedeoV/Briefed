using System.Security.Cryptography;
using System.Text;
using Briefed.Core.Entities;
using Briefed.Core.Interfaces;
using Briefed.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Briefed.Infrastructure.Services;

public class SummaryService : ISummaryService
{
    private readonly BriefedDbContext _context;
    private readonly IGroqService _groqService;
    private readonly IOllamaService _ollamaService;
    private readonly IArticleService _articleService;
    private readonly ILogger<SummaryService> _logger;
    private readonly string _defaultModel;

    public SummaryService(
        BriefedDbContext context,
        IGroqService groqService,
        IOllamaService ollamaService,
        IArticleService articleService,
        IConfiguration configuration,
        ILogger<SummaryService> logger)
    {
        _context = context;
        _groqService = groqService;
        _ollamaService = ollamaService;
        _articleService = articleService;
        _logger = logger;
        _defaultModel = configuration["Ollama:Model"] ?? "llama3.2:3b";
    }

    public async Task<Summary?> GetSummaryByArticleIdAsync(int articleId)
    {
        return await _context.Summaries
            .FirstOrDefaultAsync(s => s.ArticleId == articleId);
    }

    public async Task<Summary> GenerateSummaryAsync(int articleId, string articleContent, string summaryType = "comprehensive")
    {
        // For trending articles (articleId = 0), use trending summary cache
        if (articleId == 0)
        {
            return await GenerateTrendingSummaryAsync(articleContent, summaryType);
        }
        
        Summary? existing = await GetSummaryByArticleIdAsync(articleId);
        
        // Check if we already have this specific summary type
        if (existing != null)
        {
            var existingContent = summaryType == "concise" 
                ? existing.ConciseContent 
                : existing.ComprehensiveContent;
                
            if (!string.IsNullOrEmpty(existingContent))
            {
                _logger.LogInformation("Returning existing {SummaryType} summary for article {ArticleId}", summaryType, articleId);
                return existing;
            }
            
            _logger.LogInformation("Generating additional {SummaryType} summary for article {ArticleId}", summaryType, articleId);
        }

        string summaryText = await GenerateSummaryWithGroqAsync(articleContent, summaryType);
        string modelUsed = "Groq";

        try
        {
            if (existing != null)
            {
                // Update existing summary with the new type
                if (summaryType == "concise")
                {
                    existing.ConciseContent = summaryText;
                }
                else
                {
                    existing.ComprehensiveContent = summaryText;
                }
                
                // Keep the old Content field for backward compatibility
                if (string.IsNullOrEmpty(existing.Content))
                {
                    existing.Content = summaryText;
                }
                
                _context.Summaries.Update(existing);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Successfully updated {SummaryType} summary for article {ArticleId}", summaryType, articleId);
                return existing;
            }
            else
            {
                // Create new summary
                var summary = new Summary
                {
                    ArticleId = articleId,
                    Content = summaryText,
                    Model = modelUsed
                };
                
                if (summaryType == "concise")
                {
                    summary.ConciseContent = summaryText;
                }
                else
                {
                    summary.ComprehensiveContent = summaryText;
                }

                _context.Summaries.Add(summary);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Successfully saved {SummaryType} summary for article {ArticleId} using {Model}", summaryType, articleId, modelUsed);

                return summary;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save summary for article {ArticleId}: {Message}", articleId, ex.Message);
            throw;
        }
    }

    private async Task<Summary> GenerateTrendingSummaryAsync(string articleContent, string summaryType)
    {
        // Extract URL from content (assuming it's in the first part of the content)
        // For trending articles, we'll use a more robust approach
        var urlHash = ComputeSha256Hash(articleContent);
        
        // Check for existing trending summary
        var existing = await _context.TrendingSummaries
            .Where(ts => ts.UrlHash == urlHash && ts.ExpiresAt > DateTime.UtcNow)
            .FirstOrDefaultAsync();
        
        if (existing != null)
        {
            var existingContent = summaryType == "concise" 
                ? existing.ConciseContent 
                : existing.ComprehensiveContent;
                
            if (!string.IsNullOrEmpty(existingContent))
            {
                _logger.LogInformation("Returning cached trending summary (hash: {UrlHash}, type: {SummaryType})", urlHash.Substring(0, 8), summaryType);
                
                // Convert to Summary object for compatibility
                return new Summary
                {
                    ArticleId = 0,
                    Content = existingContent,
                    ConciseContent = existing.ConciseContent,
                    ComprehensiveContent = existing.ComprehensiveContent,
                    Model = existing.Model,
                    CreatedAt = existing.CreatedAt
                };
            }
            
            _logger.LogInformation("Generating additional {SummaryType} for cached trending article", summaryType);
        }

        // Generate new summary
        string summaryText = await GenerateSummaryWithGroqAsync(articleContent, summaryType);
        string modelUsed = "Groq";

        try
        {
            if (existing != null)
            {
                // Update existing with new summary type
                if (summaryType == "concise")
                {
                    existing.ConciseContent = summaryText;
                }
                else
                {
                    existing.ComprehensiveContent = summaryText;
                }
                
                _context.TrendingSummaries.Update(existing);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Updated trending summary cache with {SummaryType}", summaryType);
                
                return new Summary
                {
                    ArticleId = 0,
                    Content = summaryText,
                    ConciseContent = existing.ConciseContent,
                    ComprehensiveContent = existing.ComprehensiveContent,
                    Model = modelUsed,
                    CreatedAt = existing.CreatedAt
                };
            }
            else
            {
                // Create new trending summary
                var trendingSummary = new TrendingSummary
                {
                    UrlHash = urlHash,
                    Url = "trending", // Will be updated by caller if needed
                    Title = "Trending Article",
                    Model = modelUsed,
                    ExpiresAt = DateTime.UtcNow.AddDays(7)
                };
                
                if (summaryType == "concise")
                {
                    trendingSummary.ConciseContent = summaryText;
                }
                else
                {
                    trendingSummary.ComprehensiveContent = summaryText;
                }

                _context.TrendingSummaries.Add(trendingSummary);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Saved new trending summary to cache (expires in 7 days)");

                return new Summary
                {
                    ArticleId = 0,
                    Content = summaryText,
                    ConciseContent = trendingSummary.ConciseContent,
                    ComprehensiveContent = trendingSummary.ComprehensiveContent,
                    Model = modelUsed
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save trending summary: {Message}", ex.Message);
            
            // Return summary even if cache fails
            return new Summary
            {
                ArticleId = 0,
                Content = summaryText,
                Model = modelUsed
            };
        }
    }

    private async Task<string> GenerateSummaryWithGroqAsync(string content, string summaryType)
    {
        try
        {
            _logger.LogInformation("Attempting to generate summary using Groq");
            _logger.LogDebug("Content length: {Length} characters, Summary type: {SummaryType}", content.Length, summaryType);
            
            var summaryText = await _groqService.GenerateSummaryAsync(content, summaryType);
            _logger.LogInformation("Successfully generated summary using Groq");
            return summaryText;
        }
        catch (Exception groqEx)
        {
            _logger.LogWarning(groqEx, "Groq summarization failed, falling back to Ollama: {Message}", groqEx.Message);
            
            try
            {
                _logger.LogInformation("Generating summary using Ollama fallback");
                var summaryText = await _ollamaService.GenerateSummaryAsync(content, _defaultModel, summaryType);
                _logger.LogInformation("Successfully generated summary using Ollama fallback");
                return summaryText;
            }
            catch (Exception ollamaEx)
            {
                _logger.LogError(ollamaEx, "Both Groq and Ollama failed");
                throw new InvalidOperationException("Failed to generate summary with both Groq and Ollama", ollamaEx);
            }
        }
    }

    private static string ComputeSha256Hash(string rawData)
    {
        using (SHA256 sha256Hash = SHA256.Create())
        {
            byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
            {
                builder.Append(bytes[i].ToString("x2"));
            }
            return builder.ToString();
        }
    }
}
