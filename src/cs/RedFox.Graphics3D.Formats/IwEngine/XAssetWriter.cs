using CallOfFile;

namespace RedFox.Graphics3D.Formats.IwEngine;

internal sealed class XAssetWriter : IDisposable
{
    private readonly Stream destination;

    public TokenWriter Writer { get; }

    public XAssetWriter(Stream destination, bool binary)
    {
        this.destination = destination;

        Writer = binary ? new BinaryTokenWriter(destination) : new ExportTokenWriter(new StreamWriter(destination, leaveOpen: true));
    }

    public void Dispose()
    {
        Writer.Dispose();
    }
}
