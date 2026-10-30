namespace RedFox.Graphics3D.Formats.KaydaraFbx;

/// <summary>
/// Provides FBX document read and write operations that select the binary or ASCII encoding.
/// </summary>
public static class FbxDocumentIO
{
    /// <summary>
    /// Reads an FBX document from the provided stream, detecting the encoding from its contents.
    /// </summary>
    /// <param name="stream">The source FBX stream.</param>
    /// <returns>The parsed FBX document.</returns>
    public static FbxDocument Read(Stream stream)
    {
        if (stream.CanSeek)
        {
            byte[] data = new byte[stream.Length - stream.Position];
            stream.ReadExactly(data);
            return Read(data);
        }

        using MemoryStream copy = new();
        stream.CopyTo(copy);
        return Read(copy.ToArray());
    }

    /// <summary>
    /// Reads an FBX document from a byte array, detecting the encoding from its contents.
    /// </summary>
    /// <param name="data">The complete file contents.</param>
    /// <returns>The parsed FBX document.</returns>
    public static FbxDocument Read(byte[] data)
    {
        return FbxBinarySerializer.IsBinary(data) ? FbxBinarySerializer.Read(data) : FbxAsciiSerializer.Read(data);
    }

    /// <summary>
    /// Writes an FBX document to the provided stream.
    /// </summary>
    /// <param name="stream">The destination stream.</param>
    /// <param name="document">The document to write.</param>
    /// <param name="format">The target FBX format.</param>
    public static void Write(Stream stream, FbxDocument document, FbxFormat format)
    {
        if (format == FbxFormat.Binary)
        {
            FbxBinarySerializer.Write(stream, document);
        }
        else
        {
            FbxAsciiSerializer.Write(stream, document);
        }
    }
}
