# WOL Search MCP Server

A Model Context Protocol (MCP) server that enables GitHub Copilot in VS Code to search the Watchtower Online Library (WOL) for Bible study materials and Jehovah's Witnesses publications.

## What is MCP?

The **Model Context Protocol (MCP)** is a standardized protocol that allows AI assistants like GitHub Copilot to discover and interact with external tools and data sources. Think of it as a way to extend GitHub Copilot's capabilities beyond its training data by providing real-time access to APIs, databases, and services.

### Key MCP Concepts for .NET Developers

- **MCP Server**: A .NET application that exposes tools/capabilities to GitHub Copilot
- **Tools**: Methods that GitHub Copilot can discover and execute (similar to API endpoints)
- **Transport**: Communication channel between Copilot and server (typically stdio or HTTP)
- **JSON-RPC 2.0**: The underlying protocol for client-server communication

## What Makes This .NET Web API MCP-Compliant?

Unlike a traditional Web API that exposes HTTP endpoints, an MCP server:

### 1. **Uses MCP-Specific Attributes** ([Reference](https://learn.microsoft.com/en-us/dotnet/ai/quickstarts/build-mcp-server#defining-our-first-tool))
```csharp
[McpServerToolType]  // Marks a class as containing MCP tools
public static class WolSearchTool
{
    [McpServerTool, Description("Tool description for AI")]  // Exposes method as MCP tool
    public static async Task<string> Search(...)
}
```

### 2. **Follows MCP Hosting Pattern** ([Reference](https://learn.microsoft.com/en-us/dotnet/ai/quickstarts/build-mcp-server#starting-up-our-server))
```csharp
builder.Services
    .AddMcpServer()                    // Registers MCP server services
    .WithStdioServerTransport()        // Configures stdio communication
    .WithToolsFromAssembly();          // Auto-discovers tools via reflection
```

### 3. **Uses Dependency Injection for Tool Parameters** ([Reference](https://github.com/modelcontextprotocol/csharp-sdk#tools))
```csharp
public static async Task<string> Search(
    WolSearchService wolService,       // Injected service
    [Description("...")] string message,  // AI-provided parameter
    int limit = 5)                     // Optional parameter with default
```

### 4. **Communicates via stdin/stdout (Not HTTP)**
- GitHub Copilot launches the MCP server as a subprocess
- Communication happens through JSON-RPC messages over stdin/stdout
- All logging must go to stderr to avoid corrupting the protocol

## Architecture

```
┌─────────────────┐    JSON-RPC     ┌──────────────────┐    Web Scraping    ┌─────────────────┐
│  GitHub Copilot │◄──────────────► │   MCP Server     │◄─────────────────► │  WOL Website    │
│   (VS Code)     │   (stdio)       │  (.NET Console)  │   (Playwright)     │ (wol.jw.org)    │
└─────────────────┘                 └──────────────────┘                    └─────────────────┘
```

## Features

- **Real-time WOL Search**: Search Jehovah's Witnesses publications and Bible study materials
- **Configurable Results**: Limit number of results (1-10), default to 5
- **Rich Metadata**: Returns publication info, links, snippets, and occurrence counts
- **Browser Automation**: Uses Playwright for reliable web scraping
- **Error Handling**: Graceful error handling with user-friendly messages

## Getting Started

### Prerequisites

- .NET 9.0 SDK
- VS Code 1.99 or later
- GitHub Copilot subscription and extension installed

### Installation

**Publish the executable:**
   ```bash
   dotnet publish -c Release -o bin/publish
   ```

### Configuration

#### For VS Code with GitHub Copilot

**Note: MCP support in VS Code requires GitHub Copilot to be installed and enabled. VS Code 1.99 or later is required.**

Add to your `.vscode/mcp.json`:

```json
{
  "servers": {
    "wol-search": {
      "type": "stdio",
      "command": "C:\\path\\to\\your\\project\\bin\\publish\\MCP.WolSearchApi.exe",
      "args": []
    }
  }
}
```

Or add to your user `settings.json`:

```json
{
  "chat.mcp.servers": {
    "wol-search": {
      "command": "C:\\path\\to\\your\\project\\bin\\publish\\MCP.WolSearchApi.exe",
      "args": []
    }
  }
}
```

## Usage

Once configured, open VS Code and ensure GitHub Copilot is enabled with Agent Mode activated. Then ask Copilot to search WOL:

- "Search WOL for information about the Governing Body"
- "Find articles about prayer in the Watchtower publications"
- "Look up Bible study materials about love"

Copilot will automatically use the WOL search tool and present the results.

## Project Structure

```
MCP.WolSearchApi/
├── Program.cs                 # MCP server setup and tool definitions
├── Services/
│   └── WolSearchService.cs    # Web scraping service using Playwright
├── Models/
│   └── WolModels.cs          # Request/response models
└── MCP.WolSearchApi.csproj   # Project file with MCP dependencies
```

## Key Dependencies

```xml
<PackageReference Include="ModelContextProtocol" Version="0.3.0-preview.2" />
<PackageReference Include="Microsoft.Playwright" Version="1.40.0" />
<PackageReference Include="Microsoft.Extensions.Hosting" Version="9.0.6" />
```

**Important Note**: This project uses Playwright version 1.40.0 specifically for compatibility with the WOL website. Later versions of Playwright encounter security-related issues when accessing wol.jw.org. Future compatibility improvements with newer Playwright versions are desired.

## How It Works

1. **Tool Discovery**: GitHub Copilot connects and discovers available tools via MCP protocol
2. **Parameter Injection**: When Copilot calls the search tool, MCP framework injects dependencies and parameters
3. **Web Scraping**: WolSearchService uses Playwright to search wol.jw.org
4. **Response Formatting**: Results are formatted as markdown for optimal AI consumption
5. **Error Handling**: Any errors are caught and returned as user-friendly messages

## Development Tips

### Debugging MCP Servers

- Use the [MCP Inspector](https://github.com/modelcontextprotocol/inspector) to test tools
- In VS Code, use the "MCP: List Servers" command to manage server status
- Add unit tests for your service logic
- Use `stderr` for all logging (stdout is reserved for MCP protocol)

### Best Practices

- Keep tool descriptions clear and detailed for AI understanding
- Use dependency injection for testability
- Handle errors gracefully with user-friendly messages
- Follow async/await patterns for I/O operations
- Use appropriate parameter descriptions with `[Description]` attributes

## Troubleshooting

### Common Issues

1. **"Unexpected token" errors**: Usually means output is going to stdout instead of stderr
2. **"Server disconnected"**: Check that the executable path is correct in your config
3. **Initialization failures**: Ensure Playwright browsers are installed: `playwright install`
4. **WOL website access issues**: If you encounter security-related errors, ensure you're using Playwright version 1.40.0 as specified in the dependencies. Newer versions may have compatibility issues with the WOL website.

### Troubleshooting

Check VS Code MCP logs:
- Use "MCP: List Servers" command in Command Palette
- Check the Output panel for MCP-related messages
- Verify GitHub Copilot is enabled and Agent Mode is activated

## Configuration Options

You can customize the WOL search behavior by modifying `WolConfig` in `WolModels.cs`:

```csharp
public class WolConfig
{
    public bool Headless { get; set; } = true;      // Run browser in headless mode
    public int TimeoutMs { get; set; } = 30000;     // Request timeout
    public string BaseUrl { get; set; } = "https://wol.jw.org";
    public string SearchPath { get; set; } = "/en/wol/s/r1/lp-e";
}
```

## Resources

- [Model Context Protocol Documentation](https://modelcontextprotocol.io/)
- [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk)
- [Microsoft MCP Blog Post](https://devblogs.microsoft.com/dotnet/build-a-model-context-protocol-mcp-server-in-csharp/)
- [Microsoft MCP Quickstart](https://learn.microsoft.com/en-us/dotnet/ai/quickstarts/build-mcp-server)