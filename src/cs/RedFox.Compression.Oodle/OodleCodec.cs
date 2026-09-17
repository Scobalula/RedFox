using System.Runtime.InteropServices;

namespace RedFox.Compression.Oodle
{
    public unsafe class OodleCodec : CompressionCodec, IDisposable
    {
        private static nint _oodleHandle;

        private static delegate* unmanaged[Cdecl]<byte*, int, byte*, int, int, int, int, byte*, int, long, long, byte*, int, int, long> _oodleDecompress;

        public OodleCodec(string oodlePath)
        {
            _oodleHandle = NativeLibrary.Load(oodlePath);
            _oodleDecompress = (delegate* unmanaged[Cdecl]<byte*, int, byte*, int, int, int, int, byte*, int, long, long, byte*, int, int, long>)NativeLibrary.GetExport(_oodleHandle, "OodleLZ_Decompress");
        }

        /// <inheritdoc/>
        public override CompressionCodecFlags Flags => throw new NotImplementedException();

        /// <inheritdoc/>
        public override int Compress(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override int Compress(ReadOnlySpan<byte> source, Span<byte> destination, ReadOnlySpan<byte> dictionary)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override int Decompress(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            if (source.IsEmpty)
                return 0;

            fixed (byte* a = &source[0])
            {
                fixed (byte* b = &destination[0])
                {
                    return (int)_oodleDecompress(
                        a,
                        source.Length,
                        b,
                        destination.Length,
                        (int)OodleFuzzSafe.No,
                        (int)OodleCheckCRC.No,
                        (int)OodleVerbosity.No,
                        null, 0,
                        0,
                        0,
                        null, 0,
                        (int)OodleThreading.None);
                }
            }
        }

        /// <inheritdoc/>
        public override int Decompress(ReadOnlySpan<byte> source, Span<byte> destination, ReadOnlySpan<byte> dictionary)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override int GetDecompressedSize(ReadOnlySpan<byte> compressedBuffer)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override int GetMaxCompressedSize(int inputSize)
        {
            throw new NotImplementedException();
        }
    }
}
