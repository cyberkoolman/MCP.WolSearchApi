using Microsoft.Playwright;
using Microsoft.Extensions.Logging; // Add this line
using Microsoft.Extensions.Configuration; // Add this line
using MCP.WolSearch.WebApi.Models;

namespace MCP.WolSearch.WebApi.Services;

public class WolSearchService : IDisposable
{
    private readonly ILogger<WolSearchService> _logger;
    private readonly WolConfig _config;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private bool _disposed = false;

    public WolSearchService(ILogger<WolSearchService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _config = configuration.GetSection("WOL").Get<WolConfig>() ?? new WolConfig();
    }

    public async Task InitializeAsync()
    {
        try
        {
            _playwright = await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = _config.Headless,
                Args = new[] 
                {
                    "--disable-http2",
                    "--disable-blink-features=AutomationControlled",
                    "--no-first-run",
                    "--disable-dev-shm-usage",
                    "--no-sandbox",
                    "--disable-logging",
                    "--disable-web-security",
                    "--silent"
                }
            });
            
            _logger.LogDebug("WOL Search Service initialized successfully (Headless: {Headless})", _config.Headless);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize WOL Search Service");
            throw;
        }
    }

    public async Task<WolSearchResponse> SearchAsync(WolSearchRequest request)
    {
        if (_browser == null)
        {
            throw new InvalidOperationException("WOL Search Service not initialized. Call InitializeAsync() first.");
        }

        var searchUrl = BuildSearchUrl(request);
        
        try
        {
            var context = await _browser.NewContextAsync(new BrowserNewContextOptions
            {
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                ViewportSize = new ViewportSize { Width = 1920, Height = 1080 }
            });

            var page = await context.NewPageAsync();
            
            try
            {
                _logger.LogDebug("Navigating to WOL search: {SearchUrl}", searchUrl);
                await page.GotoAsync(searchUrl, new PageGotoOptions { Timeout = _config.TimeoutMs });
                
                // Wait for search results to load
                await page.WaitForSelectorAsync(".results.resultContentDocument", 
                    new PageWaitForSelectorOptions { Timeout = _config.TimeoutMs });
                
                // Extract results
                var results = await ExtractSearchResults(page, request);
                
                // Get total results count
                var totalResults = await GetTotalResultsCount(page);
                
                _logger.LogInformation("Successfully extracted {Count} results for query: '{Query}'", 
                    results.Count, request.Query);

                return new WolSearchResponse
                {
                    Query = request.Query,
                    TotalResults = totalResults,
                    Results = results,
                    SearchUrl = searchUrl,
                    Success = true
                };
            }
            finally
            {
                await page.CloseAsync();
                await context.CloseAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WOL search failed for query: '{Query}'", request.Query);
            return new WolSearchResponse
            {
                Query = request.Query,
                TotalResults = 0,
                Results = new List<WolSearchResult>(),
                SearchUrl = searchUrl,
                Success = false,
                Error = ex.Message
            };
        }
    }

    private string BuildSearchUrl(WolSearchRequest request)
    {
        var queryParams = new Dictionary<string, string>
        {
            ["q"] = request.Query,
            ["p"] = request.SearchType,
            ["r"] = request.SortBy,
            ["st"] = "a"
        };

        var queryString = string.Join("&", queryParams.Select(kvp => 
            $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));

        return $"{_config.BaseUrl}{_config.SearchPath}?{queryString}";
    }

    private async Task<List<WolSearchResult>> ExtractSearchResults(IPage page, WolSearchRequest request)
    {
        var results = new List<WolSearchResult>();
        var documentResults = await page.QuerySelectorAllAsync(".results.resultContentDocument");
        
        _logger.LogDebug("Found {Count} document result sections", documentResults.Count);
        
        int count = 0;
        foreach (var resultSection in documentResults)
        {
            if (count >= request.MaxResults) break;
            
            try
            {
                var titleLink = await resultSection.QuerySelectorAsync(".caption .lnk");
                if (titleLink == null) continue;

                var title = await titleLink.TextContentAsync() ?? "No title";
                var href = await titleLink.GetAttributeAsync("href");
                
                var link = !string.IsNullOrEmpty(href) 
                    ? $"{_config.BaseUrl}{href}?q={Uri.EscapeDataString(request.Query)}&p=par"
                    : "No link";

                // Get publication info
                var refElement = await resultSection.QuerySelectorAsync(".ref");
                var publication = refElement != null ? await refElement.TextContentAsync() ?? "" : "";

                // Get occurrence count
                var countElement = await resultSection.QuerySelectorAsync(".count");
                var occurrences = countElement != null ? await countElement.TextContentAsync() ?? "" : "";

                // Get snippet from search result content
                var snippetElement = await resultSection.QuerySelectorAsync(".searchResult .document");
                var snippet = snippetElement != null ? await snippetElement.TextContentAsync() ?? "" : "";
                
                // Clean up snippet
                if (snippet.Length > 200)
                {
                    snippet = snippet.Substring(0, 200).Trim() + "...";
                }

                // Extract year from publication if possible
                var year = ExtractYearFromPublication(publication);

                results.Add(new WolSearchResult
                {
                    Title = title.Trim(),
                    Link = link,
                    Publication = publication.Trim(),
                    Occurrences = occurrences.Trim(),
                    Year = year,
                    Snippet = snippet.Trim()
                });

                count++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error extracting result {Count}", count + 1);
            }
        }

        return results;
    }

    private async Task<int> GetTotalResultsCount(IPage page)
    {
        try
        {
            var resultsCountElement = await page.QuerySelectorAsync("#resultsCount");
            if (resultsCountElement != null)
            {
                var resultsText = await resultsCountElement.TextContentAsync();
                if (!string.IsNullOrEmpty(resultsText))
                {
                    // Extract number from text like "1651 results ( Located in the same paragraph )."
                    var match = System.Text.RegularExpressions.Regex.Match(resultsText, @"(\d+)\s+results");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int count))
                    {
                        return count;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get total results count");
        }
        
        return 0;
    }

    private static string ExtractYearFromPublication(string publication)
    {
        if (string.IsNullOrEmpty(publication)) return "";
        
        // Look for year patterns like "—1971", "—2017", etc.
        var yearMatch = System.Text.RegularExpressions.Regex.Match(publication, @"—(\d{4})");
        if (yearMatch.Success)
        {
            return yearMatch.Groups[1].Value;
        }
        
        // Look for other year patterns
        yearMatch = System.Text.RegularExpressions.Regex.Match(publication, @"\b(\d{4})\b");
        if (yearMatch.Success)
        {
            return yearMatch.Groups[1].Value;
        }
        
        return "";
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            try
            {
                _browser?.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(5));
                _playwright?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing WOL Search Service");
            }
            finally
            {
                _disposed = true;
            }
        }
    }
}