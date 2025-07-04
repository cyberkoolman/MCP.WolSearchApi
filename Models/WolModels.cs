namespace MCP.WolSearch.WebApi.Models;

public class WolSearchRequest
{
    public string Query { get; set; } = "";
    public int MaxResults { get; set; } = 5;
    public string SearchType { get; set; } = "par";
    public string SortBy { get; set; } = "occ";
}

public class WolSearchResponse
{
    public string Query { get; set; } = "";
    public int TotalResults { get; set; }
    public List<WolSearchResult> Results { get; set; } = new();
    public string SearchUrl { get; set; } = "";
    public bool Success { get; set; }
    public string Error { get; set; } = "";
}

public class WolSearchResult
{
    public string Title { get; set; } = "";
    public string Link { get; set; } = "";
    public string Publication { get; set; } = "";
    public string Occurrences { get; set; } = "";
    public string Year { get; set; } = "";
    public string Snippet { get; set; } = "";
}

public class WolConfig
{
    public bool Headless { get; set; } = true;
    public int TimeoutMs { get; set; } = 30000;
    public string BaseUrl { get; set; } = "https://wol.jw.org";
    public string SearchPath { get; set; } = "/en/wol/s/r1/lp-e";
}