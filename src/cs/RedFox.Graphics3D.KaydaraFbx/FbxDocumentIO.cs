namespace RedFox.Graphics3D.KaydaraFbx;

/// <summary>
/// Provides reusable FBX document read and write operations for ASCII and binary streams.
/// </summary>
public static class FbxDocumentIO
{
    /// <summary>
    /// Reads an FBX document from the provided stream.
    /// </summary>
    /// <param name="stream">The source FBX stream.</param>
    /// <returns>The parsed FBX document.</returns>
    public static FbxDocument Read(Stream stream)
    {
        return FbxDocumentSerializer.Read(stream);
    }

    /// <summary>
    /// Writes an FBX document to the provided stream.
    /// </summary>
    /// <param name="stream">The destination stream.</param>
    /// <param name="document">The document to write.</param>
    /// <param name="format">The target FBX format.</param>
    public static void Write(Stream stream, FbxDocument document, FbxFormat format)
    {
        FbxDocumentSerializer.Write(stream, document, format);
    }
}
