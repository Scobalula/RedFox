using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Server;

namespace RedFox.GameExtraction.Mcp.Tools;

[McpServerToolType]
internal sealed class AssetTools(AssetManager assetManager, AssetDataReader dataReader, GameExtractionMcpConfig config)
{
    [McpServerTool(Name = "search_assets"), Description("Searches mounted assets by name (substring or wildcard) and type. Returns one asset per line plus the total count.")]
    public string SearchAssets([Description("Name substring or wildcard pattern.")] string? pattern = null, [Description("Asset type, e.g. a handler-defined type name.")] string? type = null, [Description("Rows to skip.")] int offset = 0, [Description("Maximum rows to return.")] int limit = 50)
    {
        AssetFilter filter = new(string.IsNullOrEmpty(pattern) ? null : pattern, string.IsNullOrEmpty(type) ? null : type);
        List<Asset> matches = assetManager.Assets.Where(filter.Matches).ToList();
        StringBuilder builder = new();

        foreach (Asset asset in matches.Skip(offset).Take(Math.Clamp(limit, 1, 500)))
        {
            builder.AppendLine($"{asset.Name}\t{asset.Type}\t{asset.Information}");
        }

        builder.Append($"Total matches: {matches.Count}, showing from offset {offset}.");

        return builder.ToString();
    }

    [McpServerTool(Name = "get_asset"), Description("Returns the type, information, metadata and handler for an asset.")]
    public string GetAsset([Description("Full asset name.")] string name)
    {
        Asset asset = dataReader.GetAsset(name);
        IAssetHandler? handler = assetManager.FindHandler(asset, config.Configuration);
        StringBuilder builder = new();

        builder.AppendLine($"Name: {asset.Name}");
        builder.AppendLine($"Type: {asset.Type}");
        builder.AppendLine($"Information: {asset.Information}");
        builder.AppendLine($"Handler: {handler?.GetType().Name ?? "none"}");

        foreach ((string key, object? value) in asset.Metadata)
        {
            builder.AppendLine($"{key}: {value}");
        }

        return builder.ToString();
    }
}
