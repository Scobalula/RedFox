namespace RedFox.Tests.Graphics3D;

internal sealed class TranslationTestStream(byte[] data, int readLimit, Action? onWrite) : MemoryStream(data, true)
{
    public override int Read(Span<byte> buffer) => base.Read(buffer[..Math.Min(buffer.Length, readLimit)]);

    public override int Read(byte[] buffer, int offset, int count) => base.Read(buffer, offset, Math.Min(count, readLimit));

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        base.Write(buffer);
        onWrite?.Invoke();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        base.Write(buffer, offset, count);
        onWrite?.Invoke();
    }
}
