// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using System.Buffers;

namespace RedFox;

internal sealed class BytePatternScanBufferSet : IDisposable
{
    private readonly int _overlapCapacity;
    private readonly byte[] _overlapBuffer;
    private readonly byte[] _scanBuffer;
    private int _overlapLength;

    public byte[] ChunkBuffer { get; }

    public BytePatternScanBufferSet(int bufferSize, int patternLength)
    {
        ChunkBuffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        _overlapCapacity = Math.Max(0, patternLength - 1);
        if (_overlapCapacity == 0)
        {
            _overlapBuffer = [];
            _scanBuffer = [];
            return;
        }

        _overlapBuffer = ArrayPool<byte>.Shared.Rent(_overlapCapacity);
        _scanBuffer = ArrayPool<byte>.Shared.Rent(_overlapCapacity + bufferSize);
    }

    public BytePatternScanWindow BuildWindow(int bytesRead, long currentOffset)
    {
        if (_overlapLength == 0)
        {
            return new BytePatternScanWindow(ChunkBuffer.AsSpan(0, bytesRead), currentOffset);
        }

        _overlapBuffer.AsSpan(0, _overlapLength).CopyTo(_scanBuffer);
        ChunkBuffer.AsSpan(0, bytesRead).CopyTo(_scanBuffer.AsSpan(_overlapLength));
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
            currentWindowBytes.Slice(currentWindowBytes.Length - nextOverlapLength, nextOverlapLength).CopyTo(_overlapBuffer);
        }

        _overlapLength = nextOverlapLength;
    }

    public void Dispose()
    {
        ArrayPool<byte>.Shared.Return(ChunkBuffer);
        if (_overlapCapacity > 0)
        {
            ArrayPool<byte>.Shared.Return(_overlapBuffer);
            ArrayPool<byte>.Shared.Return(_scanBuffer);
        }
    }
}
