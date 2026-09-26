using System.Runtime.InteropServices;

namespace RedFox.Compression.Oodle;

/// <summary>
/// Loads the requested native Oodle library
/// and decompresses Oodle streams with it.
/// </summary>
public sealed unsafe class OodleCodec : CompressionCodec, IDisposable
{
    private readonly object _nativeLock = new();
    private nint _oodleHandle;
    private delegate* unmanaged[Cdecl]<byte*, nint, byte*, nint, int, int, int, byte*, nint, nint, void*, byte*, nint, int, nint> _oodleDecompress;

    /// <inheritdoc/>
    public override CompressionCodecFlags Flags => CompressionCodecFlags.None;

    /// <summary>
    /// Initializes an Oodle decoder
    /// using the specified native library.
    /// </summary>
    /// <param name="oodlePath">The path to the Oodle library.</param>
    public OodleCodec(string oodlePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(oodlePath);
        _oodleHandle = NativeLibrary.Load(oodlePath);
        try
        {
            _oodleDecompress = (delegate* unmanaged[Cdecl]<byte*, nint, byte*, nint, int, int, int, byte*, nint, nint, void*, byte*, nint, int, nint>)NativeLibrary.GetExport(_oodleHandle, "OodleLZ_Decompress");
        }
        catch
        {
            NativeLibrary.Free(_oodleHandle);
            _oodleHandle = 0;
            throw;
        }
    }

    /// <inheritdoc/>
    public override int Compress(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override int Compress(ReadOnlySpan<byte> source, Span<byte> destination, ReadOnlySpan<byte> dictionary)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override int Decompress(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        lock (_nativeLock)
        {
            ObjectDisposedException.ThrowIf(_oodleHandle == 0, this);
            if (source.IsEmpty)
                throw new ArgumentException("The compressed source is empty.", nameof(source));

            fixed (byte* sourcePointer = source)
            fixed (byte* destinationPointer = destination)
            {
                nint decompressedSize = _oodleDecompress(sourcePointer, source.Length, destinationPointer, destination.Length, (int)OodleFuzzSafe.Yes, (int)OodleCheckCRC.No, (int)OodleVerbosity.No, null, 0, 0, null, null, 0, (int)OodleThreading.None);
                if (decompressedSize <= 0 || decompressedSize > destination.Length)
                    throw new CompressionException("Oodle decompression failed.", "decompression", decompressedSize.ToString());

                return checked((int)decompressedSize);
            }
        }
    }

    /// <inheritdoc/>
    public override int Decompress(ReadOnlySpan<byte> source, Span<byte> destination, ReadOnlySpan<byte> dictionary)
    {
        throw new NotSupportedException();
    }

    /// <summary>
    /// Releases the handle
    /// for the native Oodle library.
    /// </summary>
    public void Dispose()
    {
        lock (_nativeLock)
        {
            if (_oodleHandle == 0)
                return;

            NativeLibrary.Free(_oodleHandle);
            _oodleHandle = 0;
            _oodleDecompress = null;
        }
    }

    /// <inheritdoc/>
    public override int GetDecompressedSize(ReadOnlySpan<byte> compressedBuffer)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override int GetMaxCompressedSize(int inputSize)
    {
        throw new NotSupportedException();
    }
}
