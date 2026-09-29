using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using RedFox.GameExtraction.Mcp.Tools;

namespace RedFox.GameExtraction.Mcp;

/// <summary>
/// Provides functionality to run a GameExtraction Model Context Protocol (MCP) server.
/// </summary>
public static class GameExtractionMcpServer
{
    /// <summary>
    /// Runs the GameExtraction MCP server with the specified configuration.
    /// The server provides tools for mounting game sources, searching and reading assets, and exporting data.
    /// </summary>
    /// <param name="config">The configuration for the MCP server.</param>
    /// <returns>A task that completes when the MCP server stops.</returns>
    /// <exception cref="ArgumentNullException">Thrown if config is null.</exception>
    public static Task RunAsync(GameExtractionMcpConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
        builder.Services.AddSingleton(config);
        builder.Services.AddSingleton(_ => config.AssetManagerFactory());
        builder.Services.AddSingleton<AssetDataReader>();
        builder.Services.AddMcpServer(options => options.ServerInfo = new Implementation { Name = config.Name, Version = config.Version }).WithStdioServerTransport().WithTools<SourceTools>().WithTools<AssetTools>().WithTools<DataTools>().WithTools<HandlerTools>();

        return builder.Build().RunAsync();
    }
}
