namespace RedFox.GameExtraction.Mcp;

/// <summary>
/// Configuration for the GameExtraction MCP server.
/// </summary>
public sealed class GameExtractionMcpConfig
{
    /// <summary>
    /// Gets or initializes a factory function that creates new AssetManager instances.
    /// </summary>
    public required Func<AssetManager> AssetManagerFactory { get; init; }

    /// <summary>
    /// Gets or initializes a factory function that creates default ExportConfiguration instances.
    /// </summary>
    public required Func<ExportConfiguration> ExportConfigurationFactory { get; init; }

    /// <summary>
    /// Gets or initializes the name of the MCP server. Defaults to "RedFox".
    /// </summary>
    public string Name { get; init; } = "RedFox";

    /// <summary>
    /// Gets or initializes the version of the MCP server. Defaults to "1.0.0".
    /// </summary>
    public string Version { get; init; } = "1.0.0";
}
