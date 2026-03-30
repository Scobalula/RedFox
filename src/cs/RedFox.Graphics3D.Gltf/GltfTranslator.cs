using RedFox.Graphics3D.IO;

namespace RedFox.Graphics3D.Gltf;

/// <summary>
/// Provides reading and writing of glTF 2.0 scene files in both JSON-based (.gltf)
/// and binary container (.glb) formats. Supports triangle meshes with positions,
/// normals, tangents, texture coordinates, vertex colors, skinning, morph targets,
/// PBR materials, skeletons, and skeletal animations.
/// <para>
/// When reading from a file path, external buffer URIs and <c>data:</c> URIs are
/// automatically resolved relative to the glTF file's directory. When reading from
/// a raw stream, only embedded buffers (GLB binary chunk or <c>data:</c> URIs) are
/// available; external <c>.bin</c> files cannot be resolved without file system context.
/// </para>
/// <para>
/// Writing always produces GLB binary format (single self-contained file).
/// </para>
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
    /// Reads scene data from the specified glTF or GLB file, automatically resolving
    /// any external buffer URIs relative to the file's directory.
    /// </summary>
    /// <param name="scene">The scene to populate.</param>
    /// <param name="filePath">The path to the glTF or GLB file.</param>
    /// <param name="options">Options that control how the scene data is read.</param>
    /// <param name="token">An optional cancellation token.</param>
    public override void Read(Scene scene, string filePath, SceneTranslatorOptions options, CancellationToken? token)
    {
        using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
        string baseDir = Path.GetDirectoryName(filePath) ?? string.Empty;
        string name = Path.GetFileNameWithoutExtension(filePath);

        GltfDocument doc = IsGlb(stream) ? GltfReader.ParseGlb(stream) : GltfReader.ParseGltf(stream, baseDir);

        GltfReader reader = new(doc, name, options);
        reader.Read(scene);
    }

    /// <summary>
    /// Reads scene data from the specified stream. For GLB streams, the binary
    /// buffer is embedded in the container. For JSON-based streams, only
    /// <c>data:</c> URIs can be resolved; external <c>.bin</c> files are unavailable.
    /// </summary>
    /// <param name="scene">The scene to populate.</param>
    /// <param name="stream">The input stream containing glTF or GLB data.</param>
    /// <param name="name">The file or scene name used for the root model node.</param>
    /// <param name="options">Options that control how the scene data is read.</param>
    /// <param name="token">An optional cancellation token.</param>
    public override void Read(Scene scene, Stream stream, string name, SceneTranslatorOptions options, CancellationToken? token)
    {
        GltfDocument doc = IsGlb(stream) ? GltfReader.ParseGlb(stream) : GltfReader.ParseGltf(stream, null);

        GltfReader reader = new(doc, Path.GetFileNameWithoutExtension(name), options);
        reader.Read(scene);
    }

    /// <summary>
    /// Writes scene data to the specified GLB file.
    /// </summary>
    /// <param name="scene">The scene to export.</param>
    /// <param name="filePath">The output GLB file path.</param>
    /// <param name="options">Options that control how the scene data is written.</param>
    /// <param name="token">An optional cancellation token.</param>
    public override void Write(Scene scene, string filePath, SceneTranslatorOptions options, CancellationToken? token)
    {
        using FileStream stream = new(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 4096);
        GltfWriter writer = new(options);
        writer.Write(scene, stream, Path.GetFileNameWithoutExtension(filePath));
    }

    /// <summary>
    /// Writes scene data to the specified stream in GLB binary format.
    /// </summary>
    /// <param name="scene">The scene to export.</param>
    /// <param name="stream">The output stream for the GLB data.</param>
    /// <param name="name">The scene name.</param>
    /// <param name="options">Options that control how the scene data is written.</param>
    /// <param name="token">An optional cancellation token.</param>
    public override void Write(Scene scene, Stream stream, string name, SceneTranslatorOptions options, CancellationToken? token)
    {
        GltfWriter writer = new(options);
        writer.Write(scene, stream, name);
    }

    /// <summary>
    /// Determines whether the specified file is valid for this translator by checking
    /// the extension and optionally the GLB magic bytes.
    /// </summary>
    /// <param name="filePath">The path of the file to validate.</param>
    /// <param name="ext">The file extension.</param>
    /// <param name="options">The translation options.</param>
    /// <param name="startOfFile">A buffer of initial bytes from the file.</param>
    /// <returns><see langword="true"/> if the file is a valid glTF or GLB file.</returns>
    public override bool IsValid(string filePath, string ext, SceneTranslatorOptions options, ReadOnlySpan<byte> startOfFile)
    {
        if (Extensions.Contains(ext))
            return true;

        // Check for GLB magic
        if (startOfFile.Length >= 4)
        {
            uint magic = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(startOfFile);
            return magic == GltfConstants.GlbMagic;
        }

        return false;
    }

    /// <summary>
    /// Checks whether the stream starts with the GLB magic number.
    /// Resets the stream position after checking.
    /// </summary>
    /// <param name="stream">The stream to check for the GLB magic number.</param>
    /// <returns><see langword="true"/> if the stream begins with the GLB magic bytes; otherwise, <see langword="false"/>.</returns>
    public static bool IsGlb(Stream stream)
    {
        if (!stream.CanSeek || stream.Length < 4)
            return false;

        long originalPosition = stream.Position;
        Span<byte> magic = stackalloc byte[4];
        int read = stream.Read(magic);
        stream.Position = originalPosition;

        if (read < 4)
            return false;

        return System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(magic) == GltfConstants.GlbMagic;
    }
}
