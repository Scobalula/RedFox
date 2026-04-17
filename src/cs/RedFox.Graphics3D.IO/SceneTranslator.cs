using System.Diagnostics.CodeAnalysis;

namespace RedFox.Graphics3D.IO;

/// <summary>
/// Provides an abstract base class for scene translators, defining the contract for reading and writing scene data.
/// </summary>
public abstract class SceneTranslator
{
    /// <summary>
    /// Gets the name of the translator, used for identification and deduplication during registration.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Gets whether the translator supports reading operations.
    /// </summary>
    public abstract bool CanRead { get; }

    /// <summary>
    /// Gets whether the translator supports writing operations.
    /// </summary>
    public abstract bool CanWrite { get; }

    /// <summary>
    /// Gets the file extensions this translator can handle, including the leading period (e.g. <c>".obj"</c>).
    /// </summary>
    public abstract IReadOnlyList<string> Extensions { get; }

    /// <summary>
    /// Gets the magic bytes used to identify the file format, or an empty span if not applicable.
    /// </summary>
    public virtual ReadOnlySpan<byte> MagicValue => [];

    /// <summary>
    /// Reads scene data from the specified file and populates the provided scene.
    /// </summary>
    /// <param name="scene">The scene to populate with data read from the file.</param>
    /// <param name="filePath">The path to the file containing the scene data.</param>
    /// <param name="options">Options that control how the scene data is read and translated.</param>
    /// <param name="token">An optional cancellation token.</param>
    public virtual void Read(Scene scene, string filePath, SceneTranslatorOptions options, CancellationToken? token)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
        Read(scene, stream, CreateReadContext(filePath, options), token);
    }

    /// <summary>
    /// Reads scene data from the specified stream using the supplied translation context.
    /// </summary>
    /// <param name="scene">The scene to populate with data read from the stream.</param>
    /// <param name="stream">The input stream containing scene data.</param>
    /// <param name="context">The translation context for this operation.</param>
    /// <param name="token">An optional cancellation token.</param>
    public abstract void Read(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? token);

    /// <summary>
    /// Reads scene data from the specified stream.
    /// </summary>
    /// <param name="scene">The scene to populate.</param>
    /// <param name="stream">The input stream containing scene data.</param>
    /// <param name="name">The logical name of the file being read.</param>
    /// <param name="options">Options that control how the scene data is read and translated.</param>
    /// <param name="token">An optional cancellation token.</param>
    public virtual void Read(Scene scene, Stream stream, string name, SceneTranslatorOptions options, CancellationToken? token)
    {
        ArgumentNullException.ThrowIfNull(options);

        Read(scene, stream, new SceneTranslationContext(name, options)
        {
            SourceFilePath = options.SourceFilePath,
            SourceDirectoryPath = options.SourceDirectoryPath,
        }, token);
    }

    /// <summary>
    /// Writes scene data to the specified file.
    /// </summary>
    /// <param name="scene">The scene to write.</param>
    /// <param name="filePath">The path to the output file.</param>
    /// <param name="options">Options that control how the scene data is written.</param>
    /// <param name="token">An optional cancellation token.</param>
    public virtual void Write(Scene scene, string filePath, SceneTranslatorOptions options, CancellationToken? token)
    {
        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.Asynchronous);
        Write(scene, stream, CreateWriteContext(filePath, options), token);
    }

    /// <summary>
    /// Writes scene data to the specified stream using the supplied translation context.
    /// </summary>
    /// <param name="scene">The scene to write.</param>
    /// <param name="stream">The output stream.</param>
    /// <param name="context">The translation context for this operation.</param>
    /// <param name="token">An optional cancellation token.</param>
    public abstract void Write(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? token);

    /// <summary>
    /// Writes scene data to the specified stream.
    /// </summary>
    /// <param name="scene">The scene to write.</param>
    /// <param name="stream">The output stream.</param>
    /// <param name="name">The logical name of the file being written.</param>
    /// <param name="options">Options that control how the scene data is written.</param>
    /// <param name="token">An optional cancellation token.</param>
    public virtual void Write(Scene scene, Stream stream, string name, SceneTranslatorOptions options, CancellationToken? token)
    {
        ArgumentNullException.ThrowIfNull(options);
        Write(scene, stream, new SceneTranslationContext(name, options), token);
    }

    /// <summary>
    /// Determines whether the specified file can be handled by this translator based on extension.
    /// </summary>
    /// <param name="filePath">The path of the file to validate.</param>
    /// <param name="ext">The file extension, including the leading period.</param>
    /// <param name="context">The translation context.</param>
    /// <returns><see langword="true"/> if this translator supports the file; otherwise, <see langword="false"/>.</returns>
    public virtual bool IsValid(string filePath, string ext, SceneTranslationContext context) =>
        IsValid(filePath, ext, context.Options);

    /// <summary>
    /// Determines whether the specified file can be handled by this translator based on extension.
    /// </summary>
    /// <param name="filePath">The path of the file to validate.</param>
    /// <param name="ext">The file extension, including the leading period.</param>
    /// <param name="options">The translation options.</param>
    /// <returns><see langword="true"/> if this translator supports the file; otherwise, <see langword="false"/>.</returns>
    public virtual bool IsValid(string filePath, string ext, SceneTranslatorOptions options) =>
        Extensions.Contains(ext);

    /// <summary>
    /// Determines whether the specified file can be handled by this translator based on extension and header magic.
    /// </summary>
    /// <param name="filePath">The path of the file to validate.</param>
    /// <param name="ext">The file extension, including the leading period.</param>
    /// <param name="context">The translation context.</param>
    /// <param name="startOfFile">Initial bytes from the start of the file.</param>
    /// <returns><see langword="true"/> if this translator supports the file; otherwise, <see langword="false"/>.</returns>
    public virtual bool IsValid(string filePath, string ext, SceneTranslationContext context, ReadOnlySpan<byte> startOfFile) =>
        IsValid(filePath, ext, context.Options, startOfFile);

    /// <summary>
    /// Determines whether the specified file can be handled by this translator based on extension and header magic.
    /// </summary>
    /// <param name="filePath">The path of the file to validate.</param>
    /// <param name="ext">The file extension, including the leading period.</param>
    /// <param name="options">The translation options.</param>
    /// <param name="startOfFile">Initial bytes from the start of the file.</param>
    /// <returns><see langword="true"/> if this translator supports the file; otherwise, <see langword="false"/>.</returns>
    public virtual bool IsValid(string filePath, string ext, SceneTranslatorOptions options, ReadOnlySpan<byte> startOfFile) =>
        IsValid(filePath, ext, options);

    /// <summary>
    /// Creates a <see cref="SceneTranslationContext"/> configured for a read operation.
    /// Sets <see cref="SceneTranslatorOptions.SourceFilePath"/> and <see cref="SceneTranslatorOptions.SourceDirectoryPath"/> on the options.
    /// </summary>
    /// <param name="filePath">The full path to the file being read.</param>
    /// <param name="options">The translation options to associate with the context.</param>
    /// <returns>A new <see cref="SceneTranslationContext"/> for the read operation.</returns>
    public static SceneTranslationContext CreateReadContext(string filePath, SceneTranslatorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        string fullPath = Path.GetFullPath(filePath);
        options.SourceFilePath = fullPath;
        options.SourceDirectoryPath = Path.GetDirectoryName(fullPath);

        return new SceneTranslationContext(Path.GetFileNameWithoutExtension(filePath), options)
        {
            SourceFilePath = fullPath,
            SourceDirectoryPath = Path.GetDirectoryName(fullPath),
        };
    }

    /// <summary>
    /// Creates a <see cref="SceneTranslationContext"/> configured for a write operation.
    /// </summary>
    /// <param name="filePath">The full path to the file being written.</param>
    /// <param name="options">The translation options to associate with the context.</param>
    /// <returns>A new <see cref="SceneTranslationContext"/> for the write operation.</returns>
    public static SceneTranslationContext CreateWriteContext(string filePath, SceneTranslatorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        string fullPath = Path.GetFullPath(filePath);
        return new SceneTranslationContext(Path.GetFileNameWithoutExtension(filePath), options)
        {
            TargetFilePath = fullPath,
            TargetDirectoryPath = Path.GetDirectoryName(fullPath),
        };
    }
}
