using Briefed.Core.Interfaces;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Briefed.Infrastructure.Services;

public class OllamaService : IOllamaService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaService> _logger;
    private readonly string _baseUrl;
    private readonly int _maxContentLength;
    private readonly int _maxTokens;

    public OllamaService(IConfiguration configuration, ILogger<OllamaService> logger)
    {
        _logger = logger;
        _baseUrl = configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
        _httpClient = new HttpClient { BaseAddress = new Uri(_baseUrl) };
        
        var timeoutValue = configuration["Ollama:TimeoutSeconds"];
        var timeout = int.TryParse(timeoutValue, out var t) ? t : 300;
        _httpClient.Timeout = TimeSpan.FromSeconds(timeout);
        
        var maxLengthValue = configuration["Ollama:MaxContentLength"];
        _maxContentLength = int.TryParse(maxLengthValue, out var maxLen) ? maxLen : 15000;
        
        var maxTokensValue = configuration["Ollama:MaxTokens"];
        _maxTokens = int.TryParse(maxTokensValue, out var mt) ? mt : 500;
    }

    public async Task<string> GenerateSummaryAsync(string text, string model = "llama3.2:3b", string summaryType = "comprehensive")
    {
        try
        {
            _logger.LogInformation("Generating summary using model {Model} at {BaseUrl}", model, _baseUrl);
            
            // Truncate very long articles to avoid token limits and excessive processing time
            var contentToSummarize = text;
            if (text.Length > _maxContentLength)
            {
                _logger.LogWarning("Article content too long ({Length} chars), truncating to {MaxLength} chars", 
                    text.Length, _maxContentLength);
                contentToSummarize = text.Substring(0, _maxContentLength) + "\n\n[Content truncated...]";
            }
            
            var promptPrefix = summaryType == "concise"
                ? "Provide a brief, concise summary highlighting only the main point of this article:"
                : "Provide a comprehensive yet concise summary of this article, capturing all key points:";
            
            var maxTokens = summaryType == "concise" ? (_maxTokens * 3) / 4 : _maxTokens;
            
            var prompt = $@"{promptPrefix}

{contentToSummarize}";

            var request = new
            {
                model = model,
                prompt = prompt,
                stream = false,
                options = new
                {
                    temperature = 0.3,
                    num_predict = maxTokens
                }
            };

            _logger.LogDebug("Sending request to Ollama API");
            var response = await _httpClient.PostAsJsonAsync("/api/generate", request);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Ollama API returned status {StatusCode}: {ErrorContent}", 
                    response.StatusCode, errorContent);
                throw new InvalidOperationException($"Ollama API returned status {response.StatusCode}: {errorContent}");
            }

            var result = await response.Content.ReadFromJsonAsync<OllamaResponse>();
            
            if (result?.Response == null)
            {
                _logger.LogError("Ollama returned an empty response");
                throw new InvalidOperationException("Ollama returned an empty response");
            }

            _logger.LogInformation("Successfully generated summary of {Length} characters", result.Response.Length);
            return result.Response.Trim();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to connect to Ollama at {BaseUrl}. Ensure Ollama is running and accessible.", _baseUrl);
            throw new InvalidOperationException($"Failed to connect to Ollama at {_baseUrl}. Please ensure the service is running.", ex);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Ollama request timed out after {Timeout} seconds", _httpClient.Timeout.TotalSeconds);
            throw new InvalidOperationException("Ollama request timed out. The article may be too long.", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Ollama response");
            throw new InvalidOperationException("Failed to parse Ollama response. The response format may be invalid.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while calling Ollama");
            throw;
        }
    }

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/tags");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private class OllamaResponse
    {
        public string? Model { get; set; }
        public string? Response { get; set; }
    }
}
