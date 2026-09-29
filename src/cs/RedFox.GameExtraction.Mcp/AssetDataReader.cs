using ModelContextProtocol;
using RedFox.IO.FileSystem;

namespace RedFox.GameExtraction.Mcp;

internal sealed class AssetDataReader(AssetManager assetManager)
{
    public Asset GetAsset(string name)
    {
        Asset? asset = assetManager.Assets.FirstOrDefault(candidate => Normalize(candidate.Name).Equals(Normalize(name), StringComparison.OrdinalIgnoreCase));

        return asset ?? throw new McpException($"Asset '{name}' was not found. Use search_assets to find valid names.");
    }

    public async Task<long> GetSizeAsync(Asset asset)
    {
        if (asset.DataSource is VirtualFile file)
        {
            return file.Size;
        }

        return (await ReadAllAsync(asset).ConfigureAwait(false)).Length;
    }

    public async Task<byte[]> ReadAsync(Asset asset, long offset, int length)
    {
        if (asset.DataSource is VirtualFile file)
        {
            using Stream stream = file.Open();

            if (stream.CanSeek)
            {
                int count = (int)Math.Clamp(Math.Min(length, stream.Length - offset), 0, length);
                byte[] buffer = new byte[count];

                stream.Seek(offset, SeekOrigin.Begin);
                stream.ReadExactly(buffer);

                return buffer;
            }
        }

        byte[] data = await ReadAllAsync(asset).ConfigureAwait(false);
        int available = (int)Math.Clamp(Math.Min(length, data.Length - offset), 0, length);

        return data.AsSpan((int)Math.Min(offset, data.Length), available).ToArray();
    }

    public async Task<byte[]> ReadAllAsync(Asset asset)
    {
        if (asset.DataSource is VirtualFile file)
        {
            using Stream stream = file.Open();
            using MemoryStream memory = new();

            await stream.CopyToAsync(memory).ConfigureAwait(false);

            return memory.ToArray();
        }

        AssetReadResult result = await assetManager.ReadAsync(asset).ConfigureAwait(false);

        return result.Data as byte[] ?? throw new McpException($"Asset '{asset.Name}' has no raw byte data; its handler returned {result.Data?.GetType().Name ?? "null"}.");
    }

    private static string Normalize(string name) => name.Replace('\\', '/');
}
