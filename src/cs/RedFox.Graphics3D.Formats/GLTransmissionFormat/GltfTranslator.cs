using RedFox.Graphics3D.IO;

namespace RedFox.Graphics3D.Formats.GLTransmissionFormat;

/// <summary>
/// Provides reading and writing of glTF 2.0 scene files in both JSON-based (.gltf) and binary container (.glb) formats.
/// </summary>
public sealed class GltfTranslator : SceneTranslator
{
    /// <inheritdoc/>
    public override string Name => "glTF";

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanWrite => true;

    /// <inheritdoc/>
    public override IReadOnlyList<string> Extensions => [".gltf", ".glb"];

    /// <inheritdoc/>
    public override ReadOnlySpan<byte> MagicValue => "glTF"u8;

    /// <summary>
    /// Reads scene data from the specified stream.
    /// </summary>
    /// <param name="scene">The scene to populate.</param>
    /// <param name="stream">The input stream containing glTF or GLB data.</param>
    /// <param name="context">The translation context for this operation.</param>
    /// <param name="token">An optional cancellation token.</param>
    public override void Read(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? token)
    {
        token?.ThrowIfCancellationRequested();
        GltfDocument doc = IsGlb(stream) ? GltfReader.ParseGlb(stream) : GltfReader.ParseGltf(stream, context.SourceDirectoryPath);

        GltfReader reader = new(doc, context.Name, context.Options, context.SourceDirectoryPath);
        reader.Read(scene, token ?? CancellationToken.None);
    }

    /// <summary>
    /// Writes scene data to the specified stream in GLB binary format.
    /// </summary>
    /// <param name="scene">The scene to export.</param>
    /// <param name="stream">The output stream for the GLB data.</param>
    /// <param name="context">The translation context for this operation.</param>
    /// <param name="token">An optional cancellation token.</param>
    public override void Write(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? token)
    {
        token?.ThrowIfCancellationRequested();
        GltfWriter writer = new(context.Options, context.TargetDirectoryPath);
        writer.Write(context.GetSelection(scene), stream, context.Name, string.Equals(Path.GetExtension(context.TargetFilePath), ".gltf", StringComparison.OrdinalIgnoreCase), token ?? CancellationToken.None);
    }

    /// <summary>
    /// Determines whether the specified file is valid for this translator by checking the extension and optionally the GLB magic bytes.
    /// </summary>
    /// <param name="filePath">The path of the file to validate.</param>
    /// <param name="extension">The file extension.</param>
    /// <param name="context">The translation context.</param>
    /// <param name="startOfFile">A buffer of initial bytes from the file.</param>
    /// <returns><see langword="true"/> if the file is a valid glTF or GLB file.</returns>
    public override bool IsValid(string filePath, string extension, SceneTranslationContext context, ReadOnlySpan<byte> startOfFile)
    {
        if (IsValid(filePath, extension, context))
            return true;

        if (startOfFile.Length >= 4)
        {
            uint magic		= System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(startOfFile);
            return magic	== GltfConstants.GlbMagic;
        }

        return false;
    }

    /// <summary>
    /// Checks whether the stream starts with the GLB magic number.
    /// </summary>
    /// <param name="stream">The stream to check for the GLB magic number.</param>
    /// <returns><see langword="true"/> if the stream begins with the GLB magic bytes; otherwise, <see langword="false"/>.</returns>
    public static bool IsGlb(Stream stream)
    {
        if (!stream.CanSeek || stream.Length < 4)
            return false;

        long originalPosition	= stream.Position;
        Span<byte> magic		= stackalloc byte[4];
        int read				= stream.ReadAtLeast(magic, magic.Length, false);
        stream.Position = originalPosition;

        if (read < 4)
            return false;

        return System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(magic) == GltfConstants.GlbMagic;
    }
}
