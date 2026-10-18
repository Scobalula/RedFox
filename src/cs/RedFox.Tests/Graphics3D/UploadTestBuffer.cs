using RedFox.Graphics3D.Rendering;

namespace RedFox.Tests.Graphics3D;

internal sealed class UploadTestBuffer(int sizeBytes, int strideBytes, BufferUsage usage, GpuBufferElementType elementType) : IGpuBuffer
{
    public byte[] Bytes { get; } = new byte[sizeBytes];
    public bool IsDisposed { get; private set; }
    public int SizeBytes => Bytes.Length;
    public int StrideBytes => strideBytes;
    public BufferUsage Usage => usage;
    public GpuBufferElementType ElementType => elementType;

    public void Dispose() => IsDisposed = true;
}
