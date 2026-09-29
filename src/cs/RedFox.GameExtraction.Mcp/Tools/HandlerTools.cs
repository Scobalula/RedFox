using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Server;
using RedFox.Graphics3D;
using RedFox.Imaging;

namespace RedFox.GameExtraction.Mcp.Tools;

[McpServerToolType]
internal sealed class HandlerTools(AssetManager assetManager, AssetDataReader dataReader, GameExtractionMcpConfig config)
{
    [McpServerTool(Name = "read_asset"), Description("Runs the asset's handler and summarizes the parsed result. Errors are returned with the full stack trace, which makes this the tool for testing a handler implementation.")]
    public async Task<string> ReadAsset([Description("Full asset name.")] string name)
    {
        Asset asset = dataReader.GetAsset(name);

        try
        {
            AssetReadResult result = await assetManager.ReadAsync(asset);

            return $"Handler: {result.Handler.GetType().Name}\n{Summarize(result.Data)}";
        }
        catch (Exception exception)
        {
            return $"Read failed: {exception}";
        }
    }

    [McpServerTool(Name = "export_assets"), Description("Exports assets matching a name pattern and type using the tool's export configuration. Returns a success count and each failure.")]
    public async Task<string> ExportAssets([Description("Name substring or wildcard pattern.")] string? pattern = null, [Description("Asset type.")] string? type = null, [Description("Output directory override.")] string? outputDirectory = null)
    {
        AssetFilter filter = new(string.IsNullOrEmpty(pattern) ? null : pattern, string.IsNullOrEmpty(type) ? null : type);
        ExportConfiguration defaults = config.ExportConfigurationFactory();
        ExportConfiguration export = new()
        {
            OutputDirectory = outputDirectory ?? defaults.OutputDirectory,
            Overwrite = defaults.Overwrite,
            ExportReferences = defaults.ExportReferences,
            PreserveDirectoryStructure = defaults.PreserveDirectoryStructure,
            Options = defaults.Options
        };

        List<string> failures = [];
        int succeeded = 0;

        foreach (Asset asset in assetManager.Assets.Where(filter.Matches).ToList())
        {
            try
            {
                await assetManager.ExportAsync(asset, export);
                succeeded++;
            }
            catch (Exception exception)
            {
                failures.Add($"{asset.Name}: {exception.Message}");
            }
        }

        return $"Exported {succeeded} assets to {export.OutputDirectory}. Failures: {failures.Count}\n{string.Join('\n', failures.Take(50))}";
    }

    private static string Summarize(object? data)
    {
        return data switch
        {
            null => "No data.",
            byte[] bytes => $"Raw bytes: {bytes.Length}",
            Image image => $"Image: {image.Width}x{image.Height}, depth {image.Depth}, array {image.ArraySize}, mips {image.MipLevels}, format {image.Format}, cubemap {image.IsCubemap}",
            Scene scene => Summarize(scene),
            _ => data.ToString() ?? data.GetType().Name
        };
    }

    private static string Summarize(Scene scene)
    {
        StringBuilder builder = new();
        List<SceneNode> nodes = scene.EnumerateDescendants().ToList();

        builder.AppendLine($"Scene: {scene.Name}, {nodes.Count} nodes");

        foreach (IGrouping<string, SceneNode> group in nodes.GroupBy(node => node.GetType().Name))
        {
            builder.AppendLine($"  {group.Key}: {group.Count()}");
        }

        foreach (Mesh mesh in nodes.OfType<Mesh>())
        {
            builder.AppendLine($"Mesh {mesh.Name}: {mesh.VertexCount} vertices, {mesh.FaceCount} faces, {mesh.UVLayerCount} UV layers, bones {mesh.Skin?.Bones.Count ?? 0}, materials [{string.Join(", ", mesh.Materials?.Select(material => material.Name) ?? [])}]");
        }

        return builder.ToString();
    }
}
