using CallOfFile;

namespace RedFox.Graphics3D.Formats.XAsset;

internal sealed class XAssetWriter : IDisposable
{
    private readonly Stream destination;
    private readonly MemoryStream buffer = new();
    public TokenWriter Writer { get; }

    public XAssetWriter(Stream destination, bool binary)
    {
        this.destination = destination;
        Writer = binary ? new BinaryTokenWriter(buffer) : new ExportTokenWriter(buffer);
    }

    public void Dispose()
    {
        Writer.Dispose();
        destination.Write(buffer.ToArray());
        buffer.Dispose();
    }
}
