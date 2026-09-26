using CallOfFile;

namespace RedFox.Graphics3D.Formats.IwEngine;

internal sealed class XAssetWriter : IDisposable
{
    private readonly Stream destination;

    private readonly MemoryStream? binaryBuffer;

    public TokenWriter Writer { get; }

    public XAssetWriter(Stream destination, bool binary)
    {
        this.destination = destination;

        if (binary)
        {
            binaryBuffer = new MemoryStream();
            Writer = new BinaryTokenWriter(binaryBuffer);
        }
        else
        {
            Writer = new ExportTokenWriter(new StreamWriter(destination, leaveOpen: true));
        }
    }

    public void Dispose()
    {
        Writer.Dispose();

        if (binaryBuffer is not null)
            destination.Write(binaryBuffer.ToArray());
    }
}
