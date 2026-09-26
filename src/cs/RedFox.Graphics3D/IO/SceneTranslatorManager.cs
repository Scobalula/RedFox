using System.Diagnostics.CodeAnalysis;

namespace RedFox.Graphics3D.IO;

/// <summary>
/// Manages scene translators and coordinates import/export operations.
/// </summary>
/// <remarks>
/// <para>
/// Translators are registered explicitly on an instance; there is no global registry or discovery.
/// </para>
/// <para>
/// When reading, a translator must support reading and is chosen by the file extension confirmed by
/// its signature (<see cref="SceneTranslator.MagicValue"/>). When no translator matches the extension,
/// a translator whose signature matches the content is used. When writing, the first writable translator
/// that accepts the extension is used. Setting <see cref="SceneTranslatorOptions.TranslatorName"/> bypasses
/// detection and uses the named translator.
/// </para>
/// </remarks>
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
    /// Attempts to find the registered translator with the specified name.
    /// </summary>
    /// <param name="name">The case-insensitive translator name.</param>
    /// <param name="translator">The matching translator, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if a translator was found; otherwise, <see langword="false"/>.</returns>
    public bool TryGetTranslator(string name, [NotNullWhen(true)] out SceneTranslator? translator)
    {
        translator = _translators.Find(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        return translator is not null;
    }

    /// <summary>
    /// Attempts to find a translator that can read the specified file.
    /// </summary>
    /// <param name="filePath">The file path, used for its extension.</param>
    /// <param name="header">Initial bytes from the start of the file.</param>
    /// <param name="options">Translation options that influence selection.</param>
    /// <param name="translator">The matching translator, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if a translator was found; otherwise, <see langword="false"/>.</returns>
    public bool TryGetReader(string filePath, ReadOnlySpan<byte> header, SceneTranslatorOptions options, [NotNullWhen(true)] out SceneTranslator? translator)
    {
        return TryGetReader(filePath, header, new SceneTranslationContext(Path.GetFileNameWithoutExtension(filePath), options), out translator);
    }

    /// <summary>
    /// Attempts to find a translator that can write the specified file.
    /// </summary>
    /// <param name="filePath">The file path, used for its extension.</param>
    /// <param name="options">Translation options that influence selection.</param>
    /// <param name="translator">The matching translator, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if a translator was found; otherwise, <see langword="false"/>.</returns>
    public bool TryGetWriter(string filePath, SceneTranslatorOptions options, [NotNullWhen(true)] out SceneTranslator? translator)
    {
        return TryGetWriter(filePath, new SceneTranslationContext(Path.GetFileNameWithoutExtension(filePath), options), out translator);
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
            throw new FileNotFoundException($"Cannot read scene file '{filePath}' because it does not exist.", filePath);

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
    /// <exception cref="IOException">The stream is unusable or no translator can read the file.</exception>
    public void Read(Stream stream, string filePath, Scene scene, SceneTranslatorOptions options, CancellationToken token)
    {
        ValidateReadArguments(stream, scene, options);

        var context = SceneTranslator.CreateReadContext(filePath, options);
        var readStart = stream.Position;

        Span<byte> header = stackalloc byte[DefaultHeaderSize];
        var headerSize = stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);
        stream.Position = readStart;

        ReadStaged(GetReader(filePath, header[..headerSize], context), stream, scene, context, token);
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
            throw new FileNotFoundException($"Cannot read scene file '{filePath}' because it does not exist.", filePath);

        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
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
    /// <exception cref="IOException">The stream is unusable or no translator can read the file.</exception>
    public async Task ReadAsync(Stream stream, string filePath, Scene scene, SceneTranslatorOptions options, CancellationToken token)
    {
        ValidateReadArguments(stream, scene, options);

        var context = SceneTranslator.CreateReadContext(filePath, options);
        var readStart = stream.Position;

        byte[] header = new byte[DefaultHeaderSize];
        var headerSize = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, token).ConfigureAwait(false);
        stream.Position = readStart;

        ReadStaged(GetReader(filePath, header.AsSpan(0, headerSize), context), stream, scene, context, token);
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
    /// <exception cref="IOException">No translator can write the file.</exception>
    public void Write(string filePath, Scene scene, SceneTranslatorOptions options, CancellationToken token)
    {
        var context = CreateWriteContext(filePath, scene, options);
        var translator = GetWriter(filePath, context);

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
    /// <param name="stream">The writable stream.</param>
    /// <param name="filePath">A virtual file path used for extension-based translator lookup.</param>
    /// <param name="scene">The scene to write.</param>
    /// <param name="options">Translation options.</param>
    /// <param name="token">Cancellation token.</param>
    /// <exception cref="IOException">No translator can write the file.</exception>
    public void Write(Stream stream, string filePath, Scene scene, SceneTranslatorOptions options, CancellationToken token)
    {
        var context = CreateWriteContext(filePath, scene, options);
        GetWriter(filePath, context).Write(scene, stream, context, token);
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
    /// <exception cref="IOException">No translator can write the file.</exception>
    public async Task WriteAsync(string filePath, Scene scene, SceneTranslatorOptions options, CancellationToken token)
    {
        var context = CreateWriteContext(filePath, scene, options);
        var translator = GetWriter(filePath, context);

        await using var stream = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.Asynchronous);
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
    /// <exception cref="IOException">No translator can write the file.</exception>
    public Task WriteAsync(Stream stream, string filePath, Scene scene, SceneTranslatorOptions options, CancellationToken token)
    {
        Write(stream, filePath, scene, options, token);
        return Task.CompletedTask;
    }

    private bool TryGetReader(string filePath, ReadOnlySpan<byte> header, SceneTranslationContext context, [NotNullWhen(true)] out SceneTranslator? translator)
    {
        if (context.Options.TranslatorName is { } name)
            return TryGetTranslator(name, out translator) && translator.CanRead;

        string extension = Path.GetExtension(filePath);

        foreach (var candidate in _translators)
        {
            if (candidate.CanRead && candidate.IsValid(filePath, extension, context, header))
            {
                translator = candidate;
                return true;
            }
        }

        foreach (var candidate in _translators)
        {
            if (candidate.CanRead && !candidate.MagicValue.IsEmpty && header.StartsWith(candidate.MagicValue))
            {
                translator = candidate;
                return true;
            }
        }

        translator = null;
        return false;
    }

    private bool TryGetWriter(string filePath, SceneTranslationContext context, [NotNullWhen(true)] out SceneTranslator? translator)
    {
        if (context.Options.TranslatorName is { } name)
            return TryGetTranslator(name, out translator) && translator.CanWrite;

        string extension = Path.GetExtension(filePath);
        translator = _translators.Find(candidate => candidate.CanWrite && candidate.IsValid(filePath, extension, context));
        return translator is not null;
    }

    private SceneTranslator GetReader(string filePath, ReadOnlySpan<byte> header, SceneTranslationContext context)
    {
        if (TryGetReader(filePath, header, context, out var translator))
            return translator;

        throw new IOException(DescribeMissingTranslator("read", filePath, context, static translator => translator.CanRead));
    }

    private SceneTranslator GetWriter(string filePath, SceneTranslationContext context)
    {
        if (TryGetWriter(filePath, context, out var translator))
            return translator;

        throw new IOException(DescribeMissingTranslator("write", filePath, context, static translator => translator.CanWrite));
    }

    private string DescribeMissingTranslator(string operation, string filePath, SceneTranslationContext context, Func<SceneTranslator, bool> supportsOperation)
    {
        if (context.Options.TranslatorName is { } name)
        {
            return TryGetTranslator(name, out var named)
                ? $"Cannot {operation} '{filePath}': translator '{named.Name}' does not support {operation}ing."
                : $"Cannot {operation} '{filePath}': no translator named '{name}' is registered.";
        }

        string extension = Path.GetExtension(filePath);
        List<SceneTranslator> claimants = _translators.FindAll(translator => translator.IsValid(filePath, extension, context));

        if (claimants.Count == 0)
            return $"Cannot {operation} '{filePath}': no registered translator handles '{extension}' files.";

        List<SceneTranslator> capable = claimants.FindAll(translator => supportsOperation(translator));

        if (capable.Count == 0)
            return $"Cannot {operation} '{filePath}': '{extension}' files are handled by {string.Join(", ", claimants.Select(translator => translator.Name))}, which cannot {operation} them.";

        return $"Cannot {operation} '{filePath}': the content does not match the signature expected by {string.Join(", ", capable.Select(translator => translator.Name))}.";
    }

    private static void ValidateReadArguments(Stream stream, Scene scene, SceneTranslatorOptions options)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(options);

        if (!stream.CanRead)
            throw new IOException("The supplied stream is not readable.");

        if (!stream.CanSeek)
            throw new IOException("The supplied stream must support seeking.");
    }

    private static void ReadStaged(SceneTranslator translator, Stream stream, Scene scene, SceneTranslationContext context, CancellationToken token)
    {
        var readContext = new SceneReadContext(scene, context.Options.Merge);
        translator.Read(readContext.Staging, stream, context, token);
        readContext.Commit();
    }

    private static SceneTranslationContext CreateWriteContext(string filePath, Scene scene, SceneTranslatorOptions options)
    {
        ArgumentNullException.ThrowIfNull(scene);

        var context = SceneTranslator.CreateWriteContext(filePath, options);
        context.GetSelection(scene);
        return context;
    }
}
