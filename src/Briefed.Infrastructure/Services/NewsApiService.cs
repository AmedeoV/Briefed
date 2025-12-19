using System.Text.Json;
using Briefed.Core.Interfaces;
using Briefed.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Briefed.Infrastructure.Services;

public class NewsApiService : INewsApiService
{
    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;
    private readonly ILogger<NewsApiService> _logger;
    private const string BaseUrl = "https://newsapi.org/v2";

    public NewsApiService(HttpClient httpClient, IConfiguration configuration, ILogger<NewsApiService> logger)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Briefed/1.0 (RSS Reader)");
        _apiKey = configuration["NewsApi:ApiKey"];
        _logger = logger;
        _logger.LogInformation("NewsApiService initialized. API Key configured: {IsConfigured}", !string.IsNullOrEmpty(_apiKey));
    }

    public async Task<List<TrendingArticle>> GetTrendingArticlesAsync(string category = "general", int count = 50)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogWarning("NewsAPI key is not configured");
            return new List<TrendingArticle>();
        }

        try
        {
            var url = $"{BaseUrl}/top-headlines?country=us&category={category}&pageSize={count}&apiKey={_apiKey}";
            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("NewsAPI request failed with status {StatusCode}: {Content}", response.StatusCode, errorContent);
                return new List<TrendingArticle>();
            }

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<NewsApiResponse>(json, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });

            return result?.Articles?.Select(a => new TrendingArticle
            {
                Title = a.Title ?? "",
                Description = a.Description ?? "",
                Url = a.Url ?? "",
                Source = a.Source?.Name ?? "",
                PublishedAt = a.PublishedAt,
                ImageUrl = a.UrlToImage,
                Category = category
            }).ToList() ?? new List<TrendingArticle>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while fetching trending articles");
            return new List<TrendingArticle>();
        }
    }

    private class NewsApiResponse
    {
        public List<NewsApiArticle>? Articles { get; set; }
    }

    private class NewsApiArticle
    {
        public NewsApiSource? Source { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Url { get; set; }
        public string? UrlToImage { get; set; }
        public DateTime PublishedAt { get; set; }
    }

    private class NewsApiSource
    {
        public string? Name { get; set; }
    }
}
