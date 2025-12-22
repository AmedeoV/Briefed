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
        // For trending articles (articleId = 0), skip database operations
        var skipDatabase = articleId == 0;
        
        Summary? existing = null;
        if (!skipDatabase)
        {
            existing = await GetSummaryByArticleIdAsync(articleId);
            
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
        }

        string summaryText;
        string modelUsed;

        try
        {
            _logger.LogInformation("Attempting to generate summary for article {ArticleId} using Groq", articleId);
            _logger.LogDebug("Content length: {Length} characters, Summary type: {SummaryType}", articleContent.Length, summaryType);
            
            summaryText = await _groqService.GenerateSummaryAsync(articleContent, summaryType);
            modelUsed = "Groq";
            _logger.LogInformation("Successfully generated summary using Groq");
        }
        catch (Exception groqEx)
        {
            _logger.LogWarning(groqEx, "Groq summarization failed, falling back to Ollama: {Message}", groqEx.Message);
            
            try
            {
                _logger.LogInformation("Generating summary for article {ArticleId} using Ollama fallback", articleId);
                summaryText = await _ollamaService.GenerateSummaryAsync(articleContent, _defaultModel, summaryType);
                modelUsed = $"Ollama-{_defaultModel}";
                _logger.LogInformation("Successfully generated summary using Ollama fallback");
            }
            catch (Exception ollamaEx)
            {
                _logger.LogError(ollamaEx, "Both Groq and Ollama failed for article {ArticleId}", articleId);
                throw new InvalidOperationException("Failed to generate summary with both Groq and Ollama", ollamaEx);
            }
        }

        // Skip database operations for trending articles
        if (skipDatabase)
        {
            _logger.LogInformation("Skipping database save for trending article (non-persisted summary)");
            var summary = new Summary
            {
                ArticleId = 0,
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
            
            return summary;
        }

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
}
