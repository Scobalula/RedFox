using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace RedFox.AppHost.Packer;

/// <summary>
/// Writes the launcher overlay: the header, splash image and Brotli compressed application files.
/// </summary>
internal static class PayloadWriter
{
    private const int BufferSize = 1 << 20;
    private const int ChunkSize	= 16 << 20;

    /// <summary>
    /// Writes the overlay to the end of the output stream.
    /// </summary>
    /// <param name="output">The launcher executable stream, positioned at its end.</param>
    /// <param name="mode">How the launcher locates the application files.</param>
    /// <param name="name">The application display name, also used for the cache folder.</param>
    /// <param name="version">The application display version.</param>
    /// <param name="entry">The entry assembly file name.</param>
    /// <param name="frameworkName">The shared framework the application requires, or empty when self-contained.</param>
    /// <param name="frameworkVersion">The minimum shared framework version, or empty when self-contained.</param>
    /// <param name="splash">The splash image bytes, or an empty array for none.</param>
    /// <param name="style">The splash screen font, colours and corner radius.</param>
    /// <param name="publishDirectory">The publish folder holding the application files.</param>
    /// <param name="excludedFile">The publish folder file replaced by the launcher.</param>
    /// <param name="quality">The Brotli quality, from 0 (fastest) to 11 (smallest).</param>
    /// <returns>The size of the compressed application files.</returns>
    public static long Write(Stream output, AppHostMode mode, string name, string version, string entry, string frameworkName, string frameworkVersion, byte[] splash, SplashStyle style, string publishDirectory, string excludedFile, int quality)
    {
        List<string> files = mode == AppHostMode.Packed ? GetFiles(publishDirectory, excludedFile) : [];

        var fileHashes		= new byte[files.Count][];
        using var hash		= IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long uncompressed	= 0;

        Parallel.For(0, files.Count, i => fileHashes[i] = HashFile(Path.Combine(publishDirectory, files[i])));

        for (int i = 0; i < files.Count; i++)
        {
            hash.AppendData(Encoding.Unicode.GetBytes(files[i]));
            hash.AppendData(fileHashes[i]);
            uncompressed += sizeof(ushort) + files[i].Length * sizeof(char) + sizeof(long) + new FileInfo(Path.Combine(publishDirectory, files[i])).Length;
        }

        string payloadId	= files.Count > 0 ? Convert.ToHexStringLower(hash.GetHashAndReset())[..16] : string.Empty;
        var writer			= new BinaryWriter(output, Encoding.Unicode, true);

        writer.Write("RFXAPP01"u8);
        writer.Write((uint)mode);
        WriteString(writer, name);
        WriteString(writer, version);
        WriteString(writer, entry);
        WriteString(writer, frameworkName);
        WriteString(writer, frameworkVersion);
        writer.Write(splash.Length);
        writer.Write(splash);
        WriteString(writer, style.FontFamily);
        writer.Write(style.Background);
        writer.Write(style.Foreground);
        writer.Write(style.SecondaryText);
        writer.Write(style.Accent);
        writer.Write(style.Border);
        writer.Write(style.CornerRadius);
        WriteString(writer, payloadId);
        writer.Write(uncompressed);
        writer.Write(files.Count);
        writer.Write(0L);
        writer.Flush();

        long payloadStart = output.Position;

        if (files.Count > 0)
        {
            Compress(files, publishDirectory, output, quality);
        }

        long payloadEnd = output.Position;

        output.Position = payloadStart - sizeof(long);
        writer.Write(payloadEnd - payloadStart);
        writer.Flush();
        output.Position = payloadEnd;
        return payloadEnd - payloadStart;
    }

    private static List<string> GetFiles(string publishDirectory, string excludedFile)
    {
        return Directory.EnumerateFiles(publishDirectory, "*", SearchOption.AllDirectories)
            .Where(file => !string.Equals(Path.GetFullPath(file), Path.GetFullPath(excludedFile), StringComparison.OrdinalIgnoreCase))
            .Select(file => Path.GetRelativePath(publishDirectory, file))
            .OrderBy(file => Path.GetExtension(file), StringComparer.OrdinalIgnoreCase)
            .ThenBy(file => file, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static byte[] HashFile(string path)
    {
        using var input = File.OpenRead(path);
        return SHA256.HashData(input);
    }

    private static void WriteString(BinaryWriter writer, string value)
    {
        writer.Write(checked((ushort)value.Length));
        writer.Write(Encoding.Unicode.GetBytes(value));
    }

    private static void Compress(List<string> files, string publishDirectory, Stream output, int quality)
    {
        var batch	= new List<byte[]>();
        var buffer	= new byte[BufferSize];
        var chunk	= new byte[ChunkSize];
        int length	= 0;

        void Append(ReadOnlySpan<byte> data)
        {
            while (!data.IsEmpty)
            {
                int count = Math.Min(data.Length, ChunkSize - length);

                data[..count].CopyTo(chunk.AsSpan(length));
                data	= data[count..];
                length	+= count;

                if (length == ChunkSize)
                {
                    batch.Add(chunk);
                    chunk	= new byte[ChunkSize];
                    length	= 0;
                }

                if (batch.Count == Environment.ProcessorCount)
                {
                    WriteBatch(batch, output, quality);
                }
            }
        }

        foreach (string file in files)
        {
            using var input	= File.OpenRead(Path.Combine(publishDirectory, file));
            var header		= new byte[sizeof(ushort) + file.Length * sizeof(char) + sizeof(long)];

            BinaryPrimitives.WriteUInt16LittleEndian(header, checked((ushort)file.Length));
            Encoding.Unicode.GetBytes(file, header.AsSpan(sizeof(ushort)));
            BinaryPrimitives.WriteInt64LittleEndian(header.AsSpan(header.Length - sizeof(long)), input.Length);
            Append(header);

            int read;
            while ((read = input.Read(buffer)) > 0)
            {
                Append(buffer.AsSpan(0, read));
            }
        }

        if (length > 0)
        {
            batch.Add(chunk[..length]);
        }

        WriteBatch(batch, output, quality);
    }

    private static void WriteBatch(List<byte[]> batch, Stream output, int quality)
    {
        var compressed = new byte[batch.Count][];

        Parallel.For(0, batch.Count, i => compressed[i] = CompressChunk(batch[i], quality));

        foreach (byte[] block in compressed)
        {
            output.Write(BitConverter.GetBytes(block.Length));
            output.Write(block);
        }

        batch.Clear();
    }

    private static byte[] CompressChunk(byte[] chunk, int quality)
    {
        var destination = new byte[BrotliEncoder.GetMaxCompressedLength(chunk.Length)];

        if (!BrotliEncoder.TryCompress(chunk, destination, out int written, quality, 22))
        {
            throw new InvalidOperationException("Brotli compression failed.");
        }

        return destination[..written];
    }
}
