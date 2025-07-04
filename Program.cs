using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;
using MCP.WolSearch.WebApi.Services;
using MCP.WolSearch.WebApi.Models;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(consoleLogOptions =>
{
    // Configure all logs to go to stderr
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

// Register the WOL search service
builder.Services.AddScoped<WolSearchService>();

await builder.Build().RunAsync();

// Define a static class to hold MCP tools.
[McpServerToolType]
public static class WolSearchTool
{
    [McpServerTool, Description("Search Watchtower Online Library (WOL) for Bible study materials, publications, and Jehovah's Witnesses content.")]
    public static async Task<string> Search(
        WolSearchService wolService,
        [Description("Search term or topic to look for in WOL")] string message,
        [Description("Maximum number of results (1-10, default: 5)")] int limit = 5)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return "❌ Please provide a search term or topic.";
            }

            // Initialize service if needed
            await wolService.InitializeAsync();

            // Limit results to reasonable number for MCP
            limit = Math.Max(1, Math.Min(10, limit));
            
            var request = new WolSearchRequest
            {
                Query = message,
                MaxResults = limit,
                SearchType = "par",
                SortBy = "occ"
            };
            
            var response = await wolService.SearchAsync(request);
            
            if (!response.Success)
            {
                return $"❌ Search failed: {response.Error}";
            }
            
            if (response.Results.Count == 0)
            {
                return $"❌ No results found for '{message}' in the Watchtower Online Library.";
            }

            // Format results for Claude
            var output = $"🔍 **WOL Search Results for '{message}'** (Found {response.Results.Count} results)\n\n";
            
            for (int i = 0; i < response.Results.Count; i++)
            {
                var result = response.Results[i];
                output += $"**{i + 1}. {result.Title}**\n";
                output += $"   📖 Publication: {result.Publication}\n";
                output += $"   🔗 Link: {result.Link}\n";
                if (!string.IsNullOrEmpty(result.Occurrences))
                {
                    output += $"   📊 {result.Occurrences}\n";
                }
                if (!string.IsNullOrEmpty(result.Snippet))
                {
                    output += $"   📝 Preview: {result.Snippet}\n";
                }
                output += "\n";
            }
            
            output += $"✅ Search completed successfully. Found {response.Results.Count} relevant publications.";
            
            return output;
        }
        catch (Exception ex)
        {
            return $"❌ Search failed: {ex.Message}";
        }
    }
}