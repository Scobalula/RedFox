using System.Diagnostics.CodeAnalysis;

namespace RedFox.Graphics3D.IO;

/// <summary>
/// Manages scene translators and coordinates import/export operations.
/// </summary>
public sealed class SceneTranslatorManager
{
    private readonly List<SceneTranslator> _translators = [];

    private const int DefaultHeaderSize = 256;

    /// <summary>
    /// Gets a read-only view of all registered translators.
    /// </summary>
    public IReadOnlyList<SceneTranslator> Translators => _translators;

    /// <summary>
    /// Registers a scene translator by type, using its parameterless constructor.
    /// Replaces any existing translator with the same name.
    /// </summary>
    /// <typeparam name="T">The type of translator to register.</typeparam>
    /// <returns>This manager.</returns>
    public SceneTranslatorManager Register<T>() where T : SceneTranslator, new()
    {
        return Register(new T());
    }

    /// <summary>
    /// Registers a scene translator instance, replacing any existing translator with the same name.
    /// </summary>
    /// <param name="translator">The translator to register.</param>
    /// <returns>This manager.</returns>
    public SceneTranslatorManager Register(SceneTranslator translator)
    {
        ArgumentNullException.ThrowIfNull(translator);
        _translators.RemoveAll(t => t.Name == translator.Name);
        _translators.Add(translator);
        return this;
    }

    /// <summary>
    /// Removes the translator with the specified name.
    /// </summary>
    /// <param name="name">The case-sensitive name of the translator to remove.</param>
    /// <returns><see langword="true"/> if a translator was removed; otherwise, <see langword="false"/>.</returns>
    public bool Unregister(string name)
    {
        return _translators.RemoveAll(t => t.Name == name) > 0;
    }

    /// <summary>
    /// Attempts to find a translator for the given file and options.
    /// </summary>
    /// <param name="filePath">Full path to the file.</param>
    /// <param name="extension">File extension including the leading period.</param>
    /// <param name="options">Translation options that influence selection.</param>
    /// <param name="translator">The matching translator, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if a translator was found; otherwise, <see langword="false"/>.</returns>
    public bool TryGetTranslator(string filePath, string extension, SceneTranslatorOptions options, [NotNullWhen(true)] out SceneTranslator? translator)
    {
        return TryGetTranslator(
            filePath,
            extension,
            new SceneTranslationContext(Path.GetFileNameWithoutExtension(filePath), options),
            out translator);
    }

    /// <summary>
    /// Attempts to find a translator for the given file and translation context.
    /// </summary>
    /// <param name="filePath">Full path to the file.</param>
    /// <param name="extension">File extension including the leading period.</param>
    /// <param name="context">Translation context.</param>
    /// <param name="translator">The matching translator, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if a translator was found; otherwise, <see langword="false"/>.</returns>
    public bool TryGetTranslator(string filePath, string extension, SceneTranslationContext context, [NotNullWhen(true)] out SceneTranslator? translator)
    {
        foreach (var candidate in _translators)
        {
            if (candidate.IsValid(filePath, extension, context))
            {
                translator = candidate;
                return true;
            }
        }

        translator = null;
        return false;
    }

    /// <summary>
    /// Attempts to find a translator using file header bytes for magic-value matching.
    /// </summary>
    /// <param name="filePath">Full path to the file.</param>
    /// <param name="extension">File extension including the leading period.</param>
    /// <param name="header">Initial bytes from the start of the file.</param>
    /// <param name="options">Translation options.</param>
    /// <param name="translator">The matching translator, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if a translator was found; otherwise, <see langword="false"/>.</returns>
    public bool TryGetTranslator(string filePath, string extension, ReadOnlySpan<byte> header, SceneTranslatorOptions options, [NotNullWhen(true)] out SceneTranslator? translator)
    {
        return TryGetTranslator(
            filePath,
            extension,
            header,
            new SceneTranslationContext(Path.GetFileNameWithoutExtension(filePath), options),
            out translator);
    }

    /// <summary>
    /// Attempts to find a translator using file header bytes and translation context.
    /// </summary>
    /// <param name="filePath">Full path to the file.</param>
    /// <param name="extension">File extension including the leading period.</param>
    /// <param name="header">Initial bytes from the start of the file.</param>
    /// <param name="context">Translation context.</param>
    /// <param name="translator">The matching translator, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if a translator was found; otherwise, <see langword="false"/>.</returns>
    public bool TryGetTranslator(string filePath, string extension, ReadOnlySpan<byte> header, SceneTranslationContext context, [NotNullWhen(true)] out SceneTranslator? translator)
    {
        foreach (var candidate in _translators)
        {
            if (candidate.IsValid(filePath, extension, context, header))
            {
                translator = candidate;
                return true;
            }
        }

        translator = null;
        return false;
    }

    /// <summary>
    /// Reads a scene from a file, returning a new <see cref="Scene"/> instance.
    /// </summary>
    /// <param name="filePath">Path to the file to read.</param>
    /// <param name="options">Translation options.</param>
    /// <returns>A new scene populated from the file.</returns>
    public Scene Read(string filePath, SceneTranslatorOptions options)
    {
        var scene = new Scene(Path.GetFileName(filePath));
        Read(filePath, scene, options, CancellationToken.None);
        return scene;
    }

    /// <summary>
    /// Reads a scene from a file, returning a new <see cref="Scene"/> instance.
    /// </summary>
    /// <param name="filePath">Path to the file to read.</param>
    /// <param name="options">Translation options.</param>
    /// <param name="token">An optional cancellation token.</param>
    /// <returns>A new scene populated from the file.</returns>
    public Scene Read(string filePath, SceneTranslatorOptions options, CancellationToken? token)
    {
        var scene = new Scene(Path.GetFileName(filePath));
        Read(filePath, scene, options, token ?? CancellationToken.None);
        return scene;
    }

    /// <summary>
    /// Reads scene data from a file into an existing <see cref="Scene"/>.
    /// </summary>
    /// <param name="filePath">Path to the file to read.</param>
    /// <param name="scene">The scene to populate.</param>
    /// <param name="options">Translation options.</param>
    public void Read(string filePath, Scene scene, SceneTranslatorOptions options)
    {
        Read(filePath, scene, options, CancellationToken.None);
    }

    /// <summary>
    /// Reads scene data from a file into an existing <see cref="Scene"/>.
    /// </summary>
    /// <param name="filePath">Path to the file to read.</param>
    /// <param name="scene">The scene to populate.</param>
    /// <param name="options">Translation options.</param>
    /// <param name="token">An optional cancellation token.</param>
    public void Read(string filePath, Scene scene, SceneTranslatorOptions options, CancellationToken? token)
    {
        Read(filePath, scene, options, token ?? CancellationToken.None);
    }

    /// <summary>
    /// Reads scene data from a file into an existing <see cref="Scene"/> with cancellation support.
    /// </summary>
    /// <param name="filePath">Path to the file to read.</param>
    /// <param name="scene">The scene to populate.</param>
    /// <param name="options">Translation options.</param>
    /// <param name="token">Cancellation token.</param>
    public void Read(string filePath, Scene scene, SceneTranslatorOptions options, CancellationToken token)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        using var stream = File.OpenRead(filePath);
        Read(stream, filePath, scene, options, token);
    }

    /// <summary>
    /// Reads a scene from a stream, returning a new <see cref="Scene"/> instance.
    /// </summary>
    /// <param name="stream">The readable, seekable stream.</param>
    /// <param name="filePath">A virtual file path used for extension-based translator lookup.</param>
    /// <param name="options">Translation options.</param>
    /// <returns>A new scene populated from the stream.</returns>
    public Scene Read(Stream stream, string filePath, SceneTranslatorOptions options)
    {
        var scene = new Scene(Path.GetFileName(filePath));
        Read(stream, filePath, scene, options, CancellationToken.None);
        return scene;
    }

    /// <summary>
    /// Reads a scene from a stream, returning a new <see cref="Scene"/> instance.
    /// </summary>
    /// <param name="stream">The readable, seekable stream.</param>
    /// <param name="filePath">A virtual file path used for extension-based translator lookup.</param>
    /// <param name="options">Translation options.</param>
    /// <param name="token">An optional cancellation token.</param>
    /// <returns>A new scene populated from the stream.</returns>
    public Scene Read(Stream stream, string filePath, SceneTranslatorOptions options, CancellationToken? token)
    {
        var scene = new Scene(Path.GetFileName(filePath));
        Read(stream, filePath, scene, options, token ?? CancellationToken.None);
        return scene;
    }

    /// <summary>
    /// Reads scene data from a stream into an existing <see cref="Scene"/>.
    /// </summary>
    /// <param name="stream">The readable, seekable stream.</param>
    /// <param name="filePath">A virtual file path used for extension-based translator lookup.</param>
    /// <param name="scene">The scene to populate.</param>
    /// <param name="options">Translation options.</param>
    public void Read(Stream stream, string filePath, Scene scene, SceneTranslatorOptions options)
    {
        Read(stream, filePath, scene, options, CancellationToken.None);
    }

    /// <summary>
    /// Reads scene data from a stream into an existing <see cref="Scene"/>.
    /// </summary>
    /// <param name="stream">The readable, seekable stream.</param>
    /// <param name="filePath">A virtual file path used for extension-based translator lookup.</param>
    /// <param name="scene">The scene to populate.</param>
    /// <param name="options">Translation options.</param>
    /// <param name="token">An optional cancellation token.</param>
    public void Read(Stream stream, string filePath, Scene scene, SceneTranslatorOptions options, CancellationToken? token)
    {
        Read(stream, filePath, scene, options, token ?? CancellationToken.None);
    }

    /// <summary>
    /// Reads scene data from a stream into an existing <see cref="Scene"/> with cancellation support.
    /// </summary>
    /// <param name="stream">The readable, seekable stream.</param>
    /// <param name="filePath">A virtual file path used for extension-based translator lookup.</param>
    /// <param name="scene">The scene to populate.</param>
    /// <param name="options">Translation options.</param>
    /// <param name="token">Cancellation token.</param>
    public void Read(Stream stream, string filePath, Scene scene, SceneTranslatorOptions options, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(options);

        if (!stream.CanRead)
            throw new IOException("The supplied stream is not readable.");
        if (!stream.CanSeek)
            throw new IOException("The supplied stream must support seeking.");

        var extension = Path.GetExtension(filePath);
        var readStart = stream.Position;
        var context = SceneTranslator.CreateReadContext(filePath, options);

        Span<byte> header = stackalloc byte[DefaultHeaderSize];
        var headerSize = stream.Read(header);

        if (!TryGetTranslator(filePath, extension, header[..headerSize], context, out var translator))
            throw new IOException($"No suitable translator found for file: {filePath}");

        stream.Position = readStart;
        translator.Read(scene, stream, context, token);
    }

    /// <summary>
    /// Asynchronously reads a scene from a file, returning a new <see cref="Scene"/> instance.
    /// </summary>
    /// <param name="filePath">Path to the file to read.</param>
    /// <param name="options">Translation options.</param>
    /// <returns>A task that produces a new scene populated from the file.</returns>
    public Task<Scene> ReadAsync(string filePath, SceneTranslatorOptions options)
    {
        return ReadAsync(filePath, options, CancellationToken.None);
    }

    /// <summary>
    /// Asynchronously reads a scene from a file with cancellation support, returning a new <see cref="Scene"/> instance.
    /// </summary>
    /// <param name="filePath">Path to the file to read.</param>
    /// <param name="options">Translation options.</param>
    /// <param name="token">Cancellation token.</param>
    /// <returns>A task that produces a new scene populated from the file.</returns>
    public async Task<Scene> ReadAsync(string filePath, SceneTranslatorOptions options, CancellationToken token)
    {
        var scene = new Scene(Path.GetFileName(filePath));
        await ReadAsync(filePath, scene, options, token).ConfigureAwait(false);
        return scene;
    }

    /// <summary>
    /// Asynchronously reads scene data from a file into an existing <see cref="Scene"/>.
    /// </summary>
    /// <param name="filePath">Path to the file to read.</param>
    /// <param name="scene">The scene to populate.</param>
    /// <param name="options">Translation options.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task ReadAsync(string filePath, Scene scene, SceneTranslatorOptions options)
    {
        return ReadAsync(filePath, scene, options, CancellationToken.None);
    }

    /// <summary>
    /// Asynchronously reads scene data from a file into an existing <see cref="Scene"/> with cancellation support.
    /// </summary>
    /// <param name="filePath">Path to the file to read.</param>
    /// <param name="scene">The scene to populate.</param>
    /// <param name="options">Translation options.</param>
    /// <param name="token">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ReadAsync(string filePath, Scene scene, SceneTranslatorOptions options, CancellationToken token)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            4096,
            FileOptions.Asynchronous);

        await ReadAsync(stream, filePath, scene, options, token).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously reads a scene from a stream, returning a new <see cref="Scene"/> instance.
    /// </summary>
    /// <param name="stream">The readable, seekable stream.</param>
    /// <param name="filePath">A virtual file path used for extension-based translator lookup.</param>
    /// <param name="options">Translation options.</param>
    /// <returns>A task that produces a new scene populated from the stream.</returns>
    public Task<Scene> ReadAsync(Stream stream, string filePath, SceneTranslatorOptions options)
    {
        return ReadAsync(stream, filePath, options, CancellationToken.None);
    }

    /// <summary>
    /// Asynchronously reads a scene from a stream with cancellation support, returning a new <see cref="Scene"/> instance.
    /// </summary>
    /// <param name="stream">The readable, seekable stream.</param>
    /// <param name="filePath">A virtual file path used for extension-based translator lookup.</param>
    /// <param name="options">Translation options.</param>
    /// <param name="token">Cancellation token.</param>
    /// <returns>A task that produces a new scene populated from the stream.</returns>
    public async Task<Scene> ReadAsync(Stream stream, string filePath, SceneTranslatorOptions options, CancellationToken token)
    {
        var scene = new Scene(Path.GetFileName(filePath));
        await ReadAsync(stream, filePath, scene, options, token).ConfigureAwait(false);
        return scene;
    }

    /// <summary>
    /// Asynchronously reads scene data from a stream into an existing <see cref="Scene"/>.
    /// </summary>
    /// <param name="stream">The readable, seekable stream.</param>
    /// <param name="filePath">A virtual file path used for extension-based translator lookup.</param>
    /// <param name="scene">The scene to populate.</param>
    /// <param name="options">Translation options.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task ReadAsync(Stream stream, string filePath, Scene scene, SceneTranslatorOptions options)
    {
        return ReadAsync(stream, filePath, scene, options, CancellationToken.None);
    }

    /// <summary>
    /// Asynchronously reads scene data from a stream into an existing <see cref="Scene"/> with cancellation support.
    /// </summary>
    /// <param name="stream">The readable, seekable stream.</param>
    /// <param name="filePath">A virtual file path used for extension-based translator lookup.</param>
    /// <param name="scene">The scene to populate.</param>
    /// <param name="options">Translation options.</param>
    /// <param name="token">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ReadAsync(Stream stream, string filePath, Scene scene, SceneTranslatorOptions options, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(options);

        if (!stream.CanRead)
            throw new IOException("The supplied stream is not readable.");
        if (!stream.CanSeek)
            throw new IOException("The supplied stream must support seeking.");

        var extension = Path.GetExtension(filePath);
        var readStart = stream.Position;
        var context = SceneTranslator.CreateReadContext(filePath, options);

        Memory<byte> header = new byte[DefaultHeaderSize];
        var headerSize = await stream.ReadAsync(header, token).ConfigureAwait(false);

        if (!TryGetTranslator(filePath, extension, header.Span[..headerSize], context, out var translator))
            throw new IOException($"No suitable translator found for file: {filePath}");

        stream.Position = readStart;
        translator.Read(scene, stream, context, token);
    }

    /// <summary>
    /// Writes a scene to a file.
    /// </summary>
    /// <param name="filePath">The output file path.</param>
    /// <param name="scene">The scene to write.</param>
    /// <param name="options">Translation options.</param>
    public void Write(string filePath, Scene scene, SceneTranslatorOptions options)
    {
        Write(filePath, scene, options, CancellationToken.None);
    }

    /// <summary>
    /// Writes a scene to a file.
    /// </summary>
    /// <param name="filePath">The output file path.</param>
    /// <param name="scene">The scene to write.</param>
    /// <param name="options">Translation options.</param>
    /// <param name="token">An optional cancellation token.</param>
    public void Write(string filePath, Scene scene, SceneTranslatorOptions options, CancellationToken? token)
    {
        Write(filePath, scene, options, token ?? CancellationToken.None);
    }

    /// <summary>
    /// Writes a scene to a file with cancellation support.
    /// </summary>
    /// <param name="filePath">The output file path.</param>
    /// <param name="scene">The scene to write.</param>
    /// <param name="options">Translation options.</param>
    /// <param name="token">Cancellation token.</param>
    public void Write(string filePath, Scene scene, SceneTranslatorOptions options, CancellationToken token)
    {
        var extension = Path.GetExtension(filePath);
        var context = SceneTranslator.CreateWriteContext(filePath, options);
        context.GetSelection(scene);

        if (!TryGetTranslator(filePath, extension, context, out var translator))
            throw new IOException($"No suitable translator found for file: {filePath}");

        using var stream = File.Create(filePath);
        translator.Write(scene, stream, context, token);
    }

    /// <summary>
    /// Writes a scene to a stream.
    /// </summary>
    /// <param name="stream">The writable stream.</param>
    /// <param name="filePath">A virtual file path used for extension-based translator lookup.</param>
    /// <param name="scene">The scene to write.</param>
    /// <param name="options">Translation options.</param>
    public void Write(Stream stream, string filePath, Scene scene, SceneTranslatorOptions options)
    {
        Write(stream, filePath, scene, options, CancellationToken.None);
    }

    /// <summary>
    /// Writes a scene to a stream.
    /// </summary>
    /// <param name="stream">The writable stream.</param>
    /// <param name="filePath">A virtual file path used for extension-based translator lookup.</param>
    /// <param name="scene">The scene to write.</param>
    /// <param name="options">Translation options.</param>
    /// <param name="token">An optional cancellation token.</param>
    public void Write(Stream stream, string filePath, Scene scene, SceneTranslatorOptions options, CancellationToken? token)
    {
        Write(stream, filePath, scene, options, token ?? CancellationToken.None);
    }

    /// <summary>
    /// Writes a scene to a stream with cancellation support.
    /// </summary>
    /// <param name="stream">The writable stream.</param />
    /// <param name="filePath">A virtual file path used for extension-based translator lookup.</param>
    /// <param name="scene">The scene to write.</param>
    /// <param name="options">Translation options.</param>
    /// <param name="token">Cancellation token.</param>
    public void Write(Stream stream, string filePath, Scene scene, SceneTranslatorOptions options, CancellationToken token)
    {
        var extension = Path.GetExtension(filePath);
        var context = SceneTranslator.CreateWriteContext(filePath, options);
        context.GetSelection(scene);

        if (!TryGetTranslator(filePath, extension, context, out var translator))
            throw new IOException($"No suitable translator found for file: {filePath}");

        translator.Write(scene, stream, context, token);
    }

    /// <summary>
    /// Asynchronously writes a scene to a file.
    /// </summary>
    /// <param name="filePath">The output file path.</param>
    /// <param name="scene">The scene to write.</param>
    /// <param name="options">Translation options.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task WriteAsync(string filePath, Scene scene, SceneTranslatorOptions options)
    {
        return WriteAsync(filePath, scene, options, CancellationToken.None);
    }

    /// <summary>
    /// Asynchronously writes a scene to a file with cancellation support.
    /// </summary>
    /// <param name="filePath">The output file path.</param>
    /// <param name="scene">The scene to write.</param>
    /// <param name="options">Translation options.</param>
    /// <param name="token">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task WriteAsync(string filePath, Scene scene, SceneTranslatorOptions options, CancellationToken token)
    {
        var extension = Path.GetExtension(filePath);
        var context = SceneTranslator.CreateWriteContext(filePath, options);
        context.GetSelection(scene);

        if (!TryGetTranslator(filePath, extension, context, out var translator))
            throw new IOException($"No suitable translator found for file: {filePath}");

        await using var stream = new FileStream(
            filePath,
            FileMode.Create,
            FileAccess.ReadWrite,
            FileShare.ReadWrite,
            4096,
            FileOptions.Asynchronous);

        translator.Write(scene, stream, context, token);
    }

    /// <summary>
    /// Asynchronously writes a scene to a stream.
    /// </summary>
    /// <param name="stream">The writable stream.</param>
    /// <param name="filePath">A virtual file path used for extension-based translator lookup.</param>
    /// <param name="scene">The scene to write.</param>
    /// <param name="options">Translation options.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task WriteAsync(Stream stream, string filePath, Scene scene, SceneTranslatorOptions options)
    {
        return WriteAsync(stream, filePath, scene, options, CancellationToken.None);
    }

    /// <summary>
    /// Asynchronously writes a scene to a stream with cancellation support.
    /// </summary>
    /// <param name="stream">The writable stream.</param>
    /// <param name="filePath">A virtual file path used for extension-based translator lookup.</param>
    /// <param name="scene">The scene to write.</param>
    /// <param name="options">Translation options.</param>
    /// <param name="token">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task WriteAsync(Stream stream, string filePath, Scene scene, SceneTranslatorOptions options, CancellationToken token)
    {
        var extension = Path.GetExtension(filePath);
        var context = SceneTranslator.CreateWriteContext(filePath, options);
        context.GetSelection(scene);

        if (!TryGetTranslator(filePath, extension, context, out var translator))
            throw new IOException($"No suitable translator found for file: {filePath}");

        translator.Write(scene, stream, context, token);
    }
}
