using System.Buffers;

namespace RedFox;

internal sealed class BytePatternScanBufferSet : IDisposable
{
    private readonly int _overlapCapacity;
    private readonly byte[] _scanBuffer;
    private int _overlapLength;

    public BytePatternScanBufferSet(int bufferSize, int patternLength)
    {
        _overlapCapacity = Math.Max(0, patternLength - 1);
        _scanBuffer = ArrayPool<byte>.Shared.Rent(_overlapCapacity + bufferSize);
    }

    public Span<byte> GetReadDestination(int readLength)
    {
        return _scanBuffer.AsSpan(_overlapLength, readLength);
    }

    public BytePatternScanWindow BuildWindow(int bytesRead, long currentOffset)
    {
        int scanLength = _overlapLength + bytesRead;
        long baseOffset = currentOffset - _overlapLength;
        return new BytePatternScanWindow(_scanBuffer.AsSpan(0, scanLength), baseOffset);
    }

    public void UpdateOverlap(ReadOnlySpan<byte> currentWindowBytes)
    {
        if (_overlapCapacity == 0)
        {
            _overlapLength = 0;
            return;
        }

        int nextOverlapLength = Math.Min(_overlapCapacity, currentWindowBytes.Length);
        if (nextOverlapLength > 0)
        {
            currentWindowBytes.Slice(currentWindowBytes.Length - nextOverlapLength, nextOverlapLength).CopyTo(_scanBuffer);
        }

        _overlapLength = nextOverlapLength;
    }

    public void Dispose()
    {
        ArrayPool<byte>.Shared.Return(_scanBuffer);
    }
}
