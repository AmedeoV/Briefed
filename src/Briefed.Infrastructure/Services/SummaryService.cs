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
        var existing = await GetSummaryByArticleIdAsync(articleId);
        if (existing != null)
        {
            _logger.LogInformation("Returning existing summary for article {ArticleId}", articleId);
            return existing;
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

        try
        {
            var summary = new Summary
            {
                ArticleId = articleId,
                Content = summaryText,
                Model = modelUsed
            };

            _context.Summaries.Add(summary);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Successfully saved summary for article {ArticleId} using {Model}", articleId, modelUsed);

            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save summary for article {ArticleId}: {Message}", articleId, ex.Message);
            throw;
        }
    }
}
