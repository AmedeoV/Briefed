using Briefed.Core.Interfaces;
using Briefed.Core.Models;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Web;

namespace Briefed.Infrastructure.Services;

public class FactCheckService : IFactCheckService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FactCheckService> _logger;
    private readonly IGroqService _groqService;
    private readonly string _apiKey;
    private readonly bool _isConfigured;

    public FactCheckService(
        IConfiguration configuration, 
        ILogger<FactCheckService> logger,
        IGroqService groqService,
        HttpClient httpClient)
    {
        _logger = logger;
        _groqService = groqService;
        _apiKey = configuration["GoogleFactCheck:ApiKey"] ?? string.Empty;
        _isConfigured = !string.IsNullOrEmpty(_apiKey);
        
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri("https://factchecktools.googleapis.com");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<bool> IsAvailableAsync()
    {
        if (!_isConfigured)
        {
            _logger.LogWarning("Google Fact Check API key not configured");
            return false;
        }

        try
        {
            // Simple availability check
            var response = await _httpClient.GetAsync($"/v1alpha1/claims:search?key={_apiKey}&query=test");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking Google Fact Check API availability");
            return false;
        }
    }

    public async Task<FactCheckResponse> CheckClaimAsync(string claim, string? languageCode = null)
    {
        if (!_isConfigured)
        {
            _logger.LogWarning("Google Fact Check API key not configured");
            return await VerifyClaimWithAIAsync(claim);
        }

        try
        {
            _logger.LogInformation("Checking claim: {Claim}", claim);

            var encodedClaim = HttpUtility.UrlEncode(claim);
            var url = $"/v1alpha1/claims:search?key={_apiKey}&query={encodedClaim}";
            
            if (!string.IsNullOrEmpty(languageCode))
            {
                url += $"&languageCode={languageCode}";
            }

            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Fact check API returned status {Status}, falling back to AI", response.StatusCode);
                return await VerifyClaimWithAIAsync(claim);
            }

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<GoogleFactCheckApiResponse>(content, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });

            if (result?.Claims == null || !result.Claims.Any())
            {
                // No existing fact-checks found, use AI to verify
                return await VerifyClaimWithAIAsync(claim);
            }

            // Get the first (most relevant) claim review
            var firstClaim = result.Claims.First();
            var firstReview = firstClaim.ClaimReview?.FirstOrDefault();

            return new FactCheckResponse
            {
                Claim = firstClaim.Text ?? claim,
                ClaimReview = firstReview?.Title ?? "No review available",
                Rating = firstReview?.TextualRating,
                Publisher = firstReview?.Publisher?.Name,
                Url = firstReview?.Url,
                TextualRating = firstReview?.TextualRating,
                ReviewDate = firstReview?.ReviewDate,
                Source = "Google Fact Check Database",
                Confidence = "High"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking claim: {Claim}, falling back to AI", claim);
            return await VerifyClaimWithAIAsync(claim);
        }
    }

    private async Task<FactCheckResponse> VerifyClaimWithAIAsync(string claim)
    {
        try
        {
            _logger.LogInformation("Using AI to verify claim: {Claim}", claim);

            var prompt = $@"You are a fact-checking assistant. Analyze this claim and provide:
1. A brief verdict (True, False, Mostly True, Mostly False, or Unverifiable)
2. Your reasoning (2-3 sentences)
3. Confidence level (High, Medium, or Low)

Format your response EXACTLY as:
VERDICT: [verdict]
REASONING: [your reasoning]
CONFIDENCE: [High/Medium/Low]

Claim: {claim}";

            var aiResponse = await _groqService.GenerateSummaryAsync(prompt);
            
            if (string.IsNullOrWhiteSpace(aiResponse))
            {
                return new FactCheckResponse
                {
                    Claim = claim,
                    ClaimReview = "Unable to verify this claim",
                    TextualRating = "Unverifiable",
                    Source = "AI Analysis",
                    Confidence = "Low",
                    Reasoning = "No analysis available"
                };
            }

            // Parse AI response
            var lines = aiResponse.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            string verdict = "Unverifiable";
            string reasoning = "Analysis provided";
            string confidence = "Medium";

            foreach (var line in lines)
            {
                if (line.StartsWith("VERDICT:", StringComparison.OrdinalIgnoreCase))
                {
                    verdict = line.Substring(8).Trim();
                }
                else if (line.StartsWith("REASONING:", StringComparison.OrdinalIgnoreCase))
                {
                    reasoning = line.Substring(10).Trim();
                }
                else if (line.StartsWith("CONFIDENCE:", StringComparison.OrdinalIgnoreCase))
                {
                    confidence = line.Substring(11).Trim();
                }
            }

            return new FactCheckResponse
            {
                Claim = claim,
                ClaimReview = reasoning,
                TextualRating = verdict,
                Rating = verdict,
                Source = "AI Analysis (Groq)",
                Confidence = confidence,
                Reasoning = reasoning
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying claim with AI: {Claim}", claim);
            return new FactCheckResponse
            {
                Claim = claim,
                ClaimReview = "Error during verification",
                TextualRating = "Unverifiable",
                Source = "AI Analysis",
                Confidence = "Low"
            };
        }
    }

    public async Task<List<FactCheckResponse>> CheckArticleAsync(string articleText)
    {
        if (!_isConfigured)
        {
            _logger.LogWarning("Google Fact Check API key not configured");
            return new List<FactCheckResponse>();
        }

        try
        {
            _logger.LogInformation("Extracting claims from article using Groq");

            // Use Groq to extract key claims from the article
            var prompt = $@"Extract 3-5 key factual claims from this article that can be fact-checked. 
Return ONLY the claims, one per line, without numbering or additional text.

Article:
{articleText.Substring(0, Math.Min(articleText.Length, 4000))}";

            var groqResponse = await _groqService.GenerateSummaryAsync(prompt);
            
            if (string.IsNullOrWhiteSpace(groqResponse))
            {
                _logger.LogWarning("No claims extracted by Groq");
                return new List<FactCheckResponse>();
            }

            // Split claims by newlines and clean them up
            var claims = groqResponse
                .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(c => c.Trim())
                .Where(c => !string.IsNullOrWhiteSpace(c) && c.Length > 10)
                .Take(5)
                .ToList();

            _logger.LogInformation("Extracted {Count} claims to fact-check", claims.Count);

            // Check each claim
            var factCheckResults = new List<FactCheckResponse>();
            foreach (var claim in claims)
            {
                var result = await CheckClaimAsync(claim);
                factCheckResults.Add(result);
                
                // Small delay to avoid rate limiting
                await Task.Delay(500);
            }

            // If no matches found, still return the extracted claims
            if (factCheckResults.All(r => r.ClaimReview?.Contains("No fact-checks found") == true || 
                                         r.ClaimReview?.Contains("not found") == true))
            {
                _logger.LogInformation("No existing fact-checks found in database for any extracted claims");
            }

            return factCheckResults;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fact-checking article");
            return new List<FactCheckResponse>();
        }
    }

    // Internal classes for API response deserialization
    private class GoogleFactCheckApiResponse
    {
        public List<GoogleFactCheckClaim>? Claims { get; set; }
    }

    private class GoogleFactCheckClaim
    {
        public string? Text { get; set; }
        public string? Claimant { get; set; }
        public DateTime? ClaimDate { get; set; }
        public List<GoogleClaimReview>? ClaimReview { get; set; }
    }

    private class GoogleClaimReview
    {
        public GooglePublisher? Publisher { get; set; }
        public string? Url { get; set; }
        public string? Title { get; set; }
        public string? TextualRating { get; set; }
        public string? LanguageCode { get; set; }
        public DateTime? ReviewDate { get; set; }
    }

    private class GooglePublisher
    {
        public string? Name { get; set; }
        public string? Site { get; set; }
    }
}
