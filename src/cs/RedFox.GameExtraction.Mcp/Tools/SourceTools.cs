using System.ComponentModel;
using ModelContextProtocol.Server;

namespace RedFox.GameExtraction.Mcp.Tools;

[McpServerToolType]
internal sealed class SourceTools(AssetManager assetManager)
{
    [McpServerTool(Name = "mount"), Description("Mounts a game file, package or directory. Wildcards in the file name are supported.")]
    public async Task<string> Mount([Description("Path to a file, directory or wildcard pattern.")] string path)
    {
        string[] paths = path.AsSpan().IndexOfAny('*', '?') >= 0 ? Directory.GetFiles(Path.GetDirectoryName(path) is { Length: > 0 } directory ? directory : ".", Path.GetFileName(path)) : [path];
        List<string> lines = [];

        foreach (string target in paths)
        {
            IAssetSource source = Directory.Exists(target) ? await assetManager.MountDirectoryAsync(target) : await assetManager.MountFileAsync(target);

            lines.Add($"{source.Name}: {source.Assets.Count} assets");
        }

        return lines.Count == 0 ? "No files matched." : string.Join('\n', lines);
    }

    [McpServerTool(Name = "mount_process"), Description("Mounts a running game process by name or process id.")]
    public async Task<string> MountProcess([Description("Process name or numeric process id.")] string nameOrId)
    {
        IAssetSource source = int.TryParse(nameOrId, out int processId) ? await assetManager.MountProcessAsync(processId) : await assetManager.MountProcessAsync(nameOrId);

        return $"{source.Name}: {source.Assets.Count} assets";
    }

    [McpServerTool(Name = "unmount"), Description("Unmounts a source by name.")]
    public async Task<string> Unmount([Description("Source name as shown by list_sources.")] string name)
    {
        IAssetSource? source = assetManager.Sources.FirstOrDefault(candidate => candidate.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (source is null)
        {
            return $"Source '{name}' is not mounted.";
        }

        await assetManager.UnloadAsync(source);

        return $"Unmounted {source.Name}.";
    }

    [McpServerTool(Name = "list_sources"), Description("Lists mounted sources and their asset counts.")]
    public string ListSources()
    {
        return assetManager.Sources.Count == 0 ? "No sources mounted." : string.Join('\n', assetManager.Sources.Select(source => $"{source.Name}: {source.Assets.Count} assets"));
    }
}
