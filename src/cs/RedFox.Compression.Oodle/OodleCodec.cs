using System.Runtime.InteropServices;

namespace RedFox.Compression.Oodle;

/// <summary>
/// Decompresses Oodle data using an application-provided Oodle library.
/// </summary>
public sealed unsafe class OodleCodec : CompressionCodec
{
    private nint _oodleHandle;
    private delegate* unmanaged[Cdecl]<byte*, nint, byte*, nint, int, int, int, byte*, nint, nint, void*, byte*, nint, int, nint> _oodleDecompress;

    /// <inheritdoc/>
    public override CompressionCodecFlags Flags => CompressionCodecFlags.Decompression;

    /// <summary>
    /// Loads the Oodle library used to decompress data.
    /// </summary>
    /// <param name="oodlePath">The path to the Oodle native library.</param>
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
    public override int Decompress(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(_oodleHandle == 0, this);
        if (source.IsEmpty)
            throw new ArgumentException("The compressed source is empty.", nameof(source));

        fixed (byte* sourcePointer = source)
        fixed (byte* destinationPointer = destination)
        {
            nint decompressedSize = _oodleDecompress(sourcePointer, source.Length, destinationPointer, destination.Length, (int)OodleFuzzSafe.Yes, (int)OodleCheckCRC.No, (int)OodleVerbosity.No, null, 0, 0, null, null, 0, (int)OodleThreading.None);
            GC.KeepAlive(this);
            if (decompressedSize <= 0 || decompressedSize > destination.Length)
                throw new CompressionException("Oodle decompression failed.", "decompression", decompressedSize.ToString());

            return checked((int)decompressedSize);
        }
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        if (_oodleHandle == 0)
            return;

        NativeLibrary.Free(_oodleHandle);
        _oodleHandle = 0;
        _oodleDecompress = null;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the loaded native library if the codec was not disposed.
    /// </summary>
    ~OodleCodec() => Dispose();
}
