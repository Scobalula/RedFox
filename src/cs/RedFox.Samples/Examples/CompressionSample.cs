// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using System.Text;
using RedFox.Compression;
using RedFox.Compression.Deflate;
using RedFox.Compression.LZ4;
using RedFox.Compression.ZStandard;

namespace RedFox.Samples.Examples;

/// <summary>
/// Demonstrates codec usage for pass-through and native codecs.
/// </summary>
internal sealed class CompressionSample : ISample
{
    /// <inheritdoc />
    public string Name => "compression";

    /// <inheritdoc />
    public string Description => "Runs pass-through and available native codec round-trips.";

    /// <inheritdoc />
    public int Run(string[] arguments)
    {
        byte[] source = Encoding.UTF8.GetBytes("RedFox compression sample payload");
        RunCodec("PassThrough", new PassThroughCodec(), source);

        if (HasNativeLibrary("miniz.dll"))
        {
            RunCodec("Deflate", new DeflateCodec(), source);
        }
        else
        {
            Console.WriteLine("Deflate skipped (Native\\miniz.dll not found).");
        }

        if (HasNativeLibrary("liblz4.dll"))
        {
            RunCodec("LZ4", new LZ4Codec(), source);
        }
        else
        {
            Console.WriteLine("LZ4 skipped (Native\\liblz4.dll not found).");
        }

        if (HasNativeLibrary("libzstd.dll"))
        {
            RunCodec("ZStandard", new ZStandardCodec(), source);
        }
        else
        {
            Console.WriteLine("ZStandard skipped (Native\\libzstd.dll not found).");
        }

        return 0;
    }

    private static void RunCodec(string name, CompressionCodec codec, byte[] source)
    {
        byte[] compressed = new byte[codec.GetMaxCompressedSize(source.Length)];
        int compressedSize = codec.Compress(source, compressed);
        byte[] decompressed = new byte[source.Length];
        int decompressedSize = codec.Decompress(compressed.AsSpan(0, compressedSize), decompressed);
        bool equal = source.AsSpan().SequenceEqual(decompressed.AsSpan(0, decompressedSize));

        Console.WriteLine($"{name}: compressed={compressedSize}, decompressed={decompressedSize}, ok={equal}");
    }

    private static bool HasNativeLibrary(string fileName)
    {
        string libraryPath = Path.Combine(AppContext.BaseDirectory, "Native", fileName);
        return File.Exists(libraryPath);
    }
}
