using Briefed.Core.Interfaces;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Briefed.Infrastructure.Services;

public class GroqService : IGroqService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GroqService> _logger;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly int _maxContentLength;
    private readonly int _maxTokens;

    public GroqService(IConfiguration configuration, ILogger<GroqService> logger)
    {
        _logger = logger;
        _apiKey = configuration["Groq:ApiKey"] ?? throw new InvalidOperationException("Groq API key not configured");
        _model = configuration["Groq:Model"] ?? "llama-3.3-70b-versatile";
        
        var timeoutValue = configuration["Groq:TimeoutSeconds"];
        var timeout = int.TryParse(timeoutValue, out var t) ? t : 30;
        
        var maxTokensValue = configuration["Groq:MaxTokens"];
        _maxTokens = int.TryParse(maxTokensValue, out var mt) ? mt : 500;
        
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.groq.com"),
            Timeout = TimeSpan.FromSeconds(timeout)
        };
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        
        // Groq is very fast, can handle longer content
        _maxContentLength = 8000;
    }

    public async Task<string> GenerateSummaryAsync(string text, string summaryType = "comprehensive")
    {
        try
        {
            _logger.LogInformation("Generating summary using Groq model {Model}", _model);
            
            // Truncate very long articles
            var contentToSummarize = text;
            if (text.Length > _maxContentLength)
            {
                _logger.LogWarning("Article content too long ({Length} chars), truncating to {MaxLength} chars", 
                    text.Length, _maxContentLength);
                contentToSummarize = text.Substring(0, _maxContentLength);
            }
            
            var systemPrompt = summaryType == "concise" 
                ? "You are a news article summarizer. Provide a brief, concise summary highlighting only the main point."
                : "You are a news article summarizer. Provide a comprehensive yet concise summary of the article, capturing all key points.";
            
            var maxTokens = summaryType == "concise" ? (_maxTokens * 3) / 4 : _maxTokens;
            
            var request = new
            {
                model = _model,
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = systemPrompt
                    },
                    new
                    {
                        role = "user",
                        content = $"Summarize this article: {contentToSummarize}"
                    }
                },
                temperature = 0.3,
                max_tokens = maxTokens
            };

            _logger.LogDebug("Sending request to Groq API");
            var response = await _httpClient.PostAsJsonAsync("/openai/v1/chat/completions", request);
            
            // Check for rate limiting
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning("Groq API rate limit exceeded");
                throw new InvalidOperationException("Groq rate limit exceeded");
            }
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Groq API returned status {StatusCode}: {ErrorContent}", 
                    response.StatusCode, errorContent);
                throw new InvalidOperationException($"Groq API returned status {response.StatusCode}: {errorContent}");
            }

            var result = await response.Content.ReadFromJsonAsync<GroqResponse>();
            
            if (result?.Choices == null || result.Choices.Length == 0 || 
                string.IsNullOrWhiteSpace(result.Choices[0].Message?.Content))
            {
                _logger.LogError("Groq returned an empty response");
                throw new InvalidOperationException("Groq returned an empty response");
            }

            var summary = result.Choices[0].Message!.Content!.Trim();
            _logger.LogInformation("Successfully generated summary of {Length} characters using Groq", summary.Length);
            return summary;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to connect to Groq API");
            throw new InvalidOperationException("Failed to connect to Groq API", ex);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Groq request timed out after {Timeout} seconds", _httpClient.Timeout.TotalSeconds);
            throw new InvalidOperationException("Groq request timed out", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Groq response");
            throw new InvalidOperationException("Failed to parse Groq response", ex);
        }
    }

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/openai/v1/models");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private class GroqResponse
    {
        public GroqChoice[]? Choices { get; set; }
        public GroqUsage? Usage { get; set; }
    }

    private class GroqChoice
    {
        public GroqMessage? Message { get; set; }
    }

    private class GroqMessage
    {
        public string? Content { get; set; }
    }

    private class GroqUsage
    {
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }
    }
}
