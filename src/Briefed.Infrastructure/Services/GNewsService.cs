using System.Text.Json;
using System.Text.Json.Serialization;
using Briefed.Core.Interfaces;
using Briefed.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Briefed.Infrastructure.Services;

public class GNewsService : IGNewsService
{
    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;
    private readonly ILogger<GNewsService> _logger;
    private const string BaseUrl = "https://gnews.io/api/v4";

    public GNewsService(HttpClient httpClient, IConfiguration configuration, ILogger<GNewsService> logger)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Briefed/1.0 (RSS Reader)");
        _apiKey = configuration["GNews:ApiKey"];
        _logger = logger;
        _logger.LogInformation("GNewsService initialized. API Key configured: {IsConfigured}", !string.IsNullOrEmpty(_apiKey));
    }

    public async Task<List<TrendingArticle>> GetTrendingArticlesAsync(string? country = null, string? category = null, int count = 10)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogWarning("GNews API key is not configured");
            return new List<TrendingArticle>();
        }

        try
        {
            // Build base URL with parameters
            var baseUrl = $"{BaseUrl}/top-headlines?token={_apiKey}&max={count}";
            
            // Add country parameter if specified
            if (!string.IsNullOrEmpty(country))
            {
                baseUrl += $"&country={country}";
            }
            
            // Add category parameter if specified
            if (!string.IsNullOrEmpty(category))
            {
                baseUrl += $"&category={category}";
            }
            
            // Try English first
            var url = baseUrl + "&lang=en";
            _logger.LogInformation("Fetching trending articles from GNews (English): {Url}", url.Replace(_apiKey, "***"));
            
            var response = await _httpClient.GetAsync(url);
            
            // Log rate limit headers if available
            if (response.Headers.Contains("X-RateLimit-Limit"))
            {
                var limit = response.Headers.GetValues("X-RateLimit-Limit").FirstOrDefault();
                var remaining = response.Headers.Contains("X-RateLimit-Remaining") 
                    ? response.Headers.GetValues("X-RateLimit-Remaining").FirstOrDefault() 
                    : "unknown";
                _logger.LogInformation("GNews API Rate Limit: {Remaining}/{Limit} requests remaining", remaining, limit);
            }
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("GNews API request failed with status {StatusCode}: {Content}", response.StatusCode, errorContent);
                return new List<TrendingArticle>();
            }

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<GNewsResponse>(json, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });

            var articles = result?.Articles?.Select(a => new TrendingArticle
            {
                Title = a.Title ?? "",
                Description = a.Description ?? "",
                Url = a.Url ?? "",
                Source = a.Source?.Name ?? "Unknown",
                PublishedAt = a.PublishedAt,
                ImageUrl = a.Image,
                Category = category ?? "general"
            }).ToList() ?? new List<TrendingArticle>();
            
            // If no English articles found and country is specified, try without language filter
            if (articles.Count == 0 && !string.IsNullOrEmpty(country))
            {
                _logger.LogInformation("No English articles found for country {Country}, trying native language", country);
                
                url = baseUrl; // Use base URL without lang parameter
                _logger.LogInformation("Fetching trending articles from GNews (native language): {Url}", url.Replace(_apiKey, "***"));
                
                response = await _httpClient.GetAsync(url);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("GNews API fallback request failed with status {StatusCode}: {Content}", response.StatusCode, errorContent);
                    return new List<TrendingArticle>();
                }

                json = await response.Content.ReadAsStringAsync();
                result = JsonSerializer.Deserialize<GNewsResponse>(json, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });

                articles = result?.Articles?.Select(a => new TrendingArticle
                {
                    Title = a.Title ?? "",
                    Description = a.Description ?? "",
                    Url = a.Url ?? "",
                    Source = a.Source?.Name ?? "Unknown",
                    PublishedAt = a.PublishedAt,
                    ImageUrl = a.Image,
                    Category = category ?? "general"
                }).ToList() ?? new List<TrendingArticle>();
                
                _logger.LogInformation("Successfully fetched {Count} trending articles from GNews (fallback to native language)", articles.Count);
            }
            else
            {
                _logger.LogInformation("Successfully fetched {Count} trending articles from GNews", articles.Count);
            }
            
            return articles;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while fetching trending articles from GNews");
            return new List<TrendingArticle>();
        }
    }

    private class GNewsResponse
    {
        [JsonPropertyName("totalArticles")]
        public int TotalArticles { get; set; }
        
        [JsonPropertyName("articles")]
        public List<GNewsArticle>? Articles { get; set; }
    }

    private class GNewsArticle
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }
        
        [JsonPropertyName("description")]
        public string? Description { get; set; }
        
        [JsonPropertyName("content")]
        public string? Content { get; set; }
        
        [JsonPropertyName("url")]
        public string? Url { get; set; }
        
        [JsonPropertyName("image")]
        public string? Image { get; set; }
        
        [JsonPropertyName("publishedAt")]
        public DateTime PublishedAt { get; set; }
        
        [JsonPropertyName("source")]
        public GNewsSource? Source { get; set; }
    }

    private class GNewsSource
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
        
        [JsonPropertyName("url")]
        public string? Url { get; set; }
    }
}
