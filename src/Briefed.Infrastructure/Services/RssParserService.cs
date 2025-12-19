using Briefed.Core.Entities;
using Briefed.Core.Interfaces;
using CodeHollow.FeedReader;
using System.Text.RegularExpressions;
using System.Net.Http;
using Microsoft.Extensions.Logging;

namespace Briefed.Infrastructure.Services;

public class RssParserService : IRssParserService
{
    private readonly ILogger<RssParserService>? _logger;
    
    public RssParserService(ILogger<RssParserService>? logger = null)
    {
        _logger = logger;
    }
    
    private static readonly HttpClient _httpClient = new(new HttpClientHandler
    {
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 5,
        AutomaticDecompression = System.Net.DecompressionMethods.All,
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        SslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13
    })
    {
        Timeout = TimeSpan.FromSeconds(90)
    };

    static RssParserService()
    {
        // Set a proper user agent to avoid being blocked
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/rss+xml, application/xml, application/atom+xml, text/xml, */*");
    }

    public async Task<(IEnumerable<Article> Articles, string? FeedTitle, string? FeedDescription, string? SiteUrl)> ParseFeedAsync(string feedUrl)
    {
        try
        {
            // Download the feed content with retry logic
            string feedContent = string.Empty;
            Exception? lastException = null;
            var maxRetries = 3;
            var downloadSucceeded = false;
            
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, feedUrl);
                    var response = await _httpClient.SendAsync(request);
                    
                    // HttpClient with AllowAutoRedirect handles redirects automatically
                    // Only throw on actual errors (not redirects)
                    if (!response.IsSuccessStatusCode)
                    {
                        // If we still get a redirect status, it means max redirections was exceeded
                        if ((int)response.StatusCode >= 300 && (int)response.StatusCode < 400)
                        {
                            throw new InvalidOperationException($"Too many redirects or redirect loop detected. Final status: {(int)response.StatusCode} {response.StatusCode}.");
                        }
                        throw new InvalidOperationException($"Server returned {(int)response.StatusCode} {response.StatusCode}. The feed may be unavailable.");
                    }
                    
                    feedContent = await response.Content.ReadAsStringAsync();
                    
                    if (string.IsNullOrWhiteSpace(feedContent))
                    {
                        throw new InvalidOperationException($"Server returned empty content.");
                    }
                    
                    // Success!
                    downloadSucceeded = true;
                    break;
                }
                catch (HttpRequestException ex) when (attempt < maxRetries)
                {
                    lastException = ex;
                    await Task.Delay(1000 * attempt); // Exponential backoff
                    continue;
                }
                catch (TaskCanceledException ex) when (attempt < maxRetries)
                {
                    lastException = ex;
                    await Task.Delay(1000 * attempt);
                    continue;
                }
                catch (HttpRequestException ex)
                {
                    var errorDetails = ex.InnerException?.Message ?? ex.Message;
                    
                    // Check for specific error types
                    if (errorDetails.Contains("404"))
                    {
                        throw new InvalidOperationException($"Feed not found (404). The feed URL may have moved or been removed. Please check the website for an updated RSS feed URL.", ex);
                    }
                    else if (errorDetails.Contains("403"))
                    {
                        throw new InvalidOperationException($"Access forbidden (403). The feed may be blocking automated requests or require authentication.", ex);
                    }
                    else if (errorDetails.Contains("SSL") || errorDetails.Contains("TLS") || errorDetails.Contains("certificate"))
                    {
                        throw new InvalidOperationException($"SSL/TLS connection error. The feed server may have certificate issues or use outdated security protocols.", ex);
                    }
                    
                    throw new InvalidOperationException($"Failed to download feed after {maxRetries} attempts. Error: {errorDetails}. The feed may be dead, moved, or blocking automated requests.", ex);
                }
                catch (TaskCanceledException ex)
                {
                    throw new InvalidOperationException($"Request timed out after {maxRetries} attempts. The feed server may be slow or unreachable.", ex);
                }
            }
            
            if (!downloadSucceeded)
            {
                throw new InvalidOperationException($"Failed to download feed after {maxRetries} attempts.", lastException);
            }

            // Check if content looks like HTML instead of XML/RSS
            var trimmedContent = feedContent.TrimStart();
            if (trimmedContent.StartsWith("<!DOCTYPE html", StringComparison.OrdinalIgnoreCase) ||
                trimmedContent.StartsWith("<html", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"The URL returned HTML instead of an RSS/Atom feed. This might be a website URL instead of a feed URL.");
            }

            // Check if it looks like XML at all
            if (!trimmedContent.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) &&
                !trimmedContent.StartsWith("<rss", StringComparison.OrdinalIgnoreCase) &&
                !trimmedContent.StartsWith("<feed", StringComparison.OrdinalIgnoreCase) &&
                !trimmedContent.StartsWith("<", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Content doesn't appear to be XML/RSS. Got: {trimmedContent.Substring(0, Math.Min(100, trimmedContent.Length))}");
            }

            // Fix common XML issues
            feedContent = FixXmlIssues(feedContent);

            // Parse the feed using FeedReader
            CodeHollow.FeedReader.Feed feed;
            try
            {
                feed = FeedReader.ReadFromString(feedContent);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"FeedReader failed to parse. Error: {ex.Message}", ex);
            }
            
            var articles = feed.Items.Select(item =>
            {
                var description = StripHtml(item.Description ?? item.Content);
                // Truncate to max 50000 characters to match database limit
                if (description?.Length > 50000)
                {
                    description = description.Substring(0, 50000);
                }
                
                // Try to get the publication date from various fields
                DateTime publishedAt = DateTime.UtcNow;
                if (item.PublishingDate.HasValue)
                {
                    publishedAt = item.PublishingDate.Value;
                }
                else if (item.PublishingDateString != null)
                {
                    // Try to parse the date string
                    if (DateTime.TryParse(item.PublishingDateString, out var parsed))
                    {
                        publishedAt = parsed;
                    }
                    else
                    {
                        _logger?.LogWarning("Could not parse date string: {DateString} for article: {Title}", 
                            item.PublishingDateString, item.Title);
                    }
                }
                else if (item.SpecificItem?.Element != null)
                {
                    // Try to extract from XML element directly (for Atom feeds)
                    var element = item.SpecificItem.Element;
                    var updatedElement = element.Descendants().FirstOrDefault(e => e.Name.LocalName == "updated");
                    var publishedElement = element.Descendants().FirstOrDefault(e => e.Name.LocalName == "published");
                    
                    var dateString = updatedElement?.Value ?? publishedElement?.Value;
                    if (dateString != null && DateTime.TryParse(dateString, out var parsed))
                    {
                        // Ensure the DateTime is UTC
                        publishedAt = parsed.Kind == DateTimeKind.Utc ? parsed : DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
                        _logger?.LogInformation("Extracted date from XML element for article: {Title} - {Date}", item.Title, publishedAt);
                    }
                    else
                    {
                        _logger?.LogWarning("No publication date found for article: {Title}, using current time", item.Title);
                    }
                }
                else
                {
                    _logger?.LogWarning("No publication date found for article: {Title}, using current time", item.Title);
                }
                
                return new Article
                {
                    Title = item.Title ?? "Untitled",
                    Url = item.Link ?? feedUrl,
                    Description = description,
                    Author = item.Author,
                    PublishedAt = publishedAt,
                    ImageUrl = item.SpecificItem?.Element?.Descendants()
                        .FirstOrDefault(e => e.Name.LocalName == "thumbnail" || e.Name.LocalName == "image")
                        ?.Attribute("url")?.Value
                };
            }).ToList();

            _logger?.LogInformation("Parsed {Count} articles from feed {FeedUrl}", articles.Count, feedUrl);

            // Get site URL from feed link or feed URL
            string? siteUrl = feed.Link;
            if (string.IsNullOrEmpty(siteUrl))
            {
                try
                {
                    var uri = new Uri(feedUrl);
                    siteUrl = $"{uri.Scheme}://{uri.Host}";
                }
                catch
                {
                    siteUrl = null;
                }
            }

            return (articles, feed.Title, feed.Description, siteUrl);
        }
        catch (InvalidOperationException)
        {
            // Re-throw our custom exceptions with better messages
            throw;
        }
        catch (Exception ex) when (ex.Message.Contains("Uri scheme"))
        {
            throw new InvalidOperationException($"Invalid feed URL format. Make sure the URL starts with http:// or https://", ex);
        }
        catch (Exception ex)
        {
            var detailedError = ex.InnerException?.Message ?? ex.Message;
            throw new InvalidOperationException($"Unable to parse feed. This could mean: (1) Invalid feed format, (2) Feed requires authentication, or (3) Feed is temporarily unavailable. Technical details: {detailedError}", ex);
        }
    }

    private static string FixXmlIssues(string xmlContent)
    {
        // Fix lowercase DOCTYPE issues
        xmlContent = Regex.Replace(xmlContent, @"<!\s*doctype\s+", "<!DOCTYPE ", RegexOptions.IgnoreCase);
        
        // Remove BOM if present
        if (xmlContent.Length > 0 && xmlContent[0] == '\uFEFF')
        {
            xmlContent = xmlContent.Substring(1);
        }

        // Trim any leading whitespace before XML declaration
        xmlContent = xmlContent.TrimStart();

        return xmlContent;
    }

    private static string? StripHtml(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var cleaned = input;
        
        // Decode HTML entities first
        cleaned = System.Net.WebUtility.HtmlDecode(cleaned);
        
        // Remove all HTML tags (loop until no more tags found)
        string previous;
        do
        {
            previous = cleaned;
            cleaned = Regex.Replace(cleaned, @"<[^>]*>", string.Empty);
        } while (previous != cleaned);
        
        // Clean up extra whitespace
        cleaned = Regex.Replace(cleaned, @"\s+", " ");
        cleaned = cleaned.Trim();
        
        return cleaned;
    }
}
