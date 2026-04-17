namespace RedFox.GameExtraction;

/// <summary>
/// Describes a source that should be mounted into an <see cref="AssetManager"/>.
/// </summary>
public sealed class AssetSourceRequest
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyOptions =
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the kind of source represented by the request.
    /// </summary>
    public AssetSourceKind Kind { get; }

    /// <summary>
    /// Gets the absolute file-system location for file and directory sources.
    /// </summary>
    public string? Location { get; }

    /// <summary>
    /// Gets the target process identifier for process sources when available.
    /// </summary>
    public int? ProcessId { get; }

    /// <summary>
    /// Gets the target process name for process sources when available.
    /// </summary>
    public string? ProcessName { get; }

    /// <summary>
    /// Gets the display name of the requested source.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets arbitrary per-mount options such as keys, name tables, or process hints.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Options { get; }

    /// <summary>
    /// Gets sampled header bytes for the request when available.
    /// </summary>
    /// <remarks>This is populated by the manager before source readers are evaluated and is empty for non-file requests.</remarks>
    public ReadOnlyMemory<byte> Header { get; internal set; }

    /// <summary>
    /// Gets sampled header bytes for the request as a span.
    /// </summary>
    public ReadOnlySpan<byte> HeaderSpan => Header.Span;

    /// <summary>
    /// Gets a human-readable description of the request.
    /// </summary>
    public string Description => Kind switch
    {
        AssetSourceKind.File => $"file '{DisplayName}'",
        AssetSourceKind.Directory => $"directory '{DisplayName}'",
        AssetSourceKind.Process when ProcessId.HasValue => $"process '{DisplayName}' (PID {ProcessId.Value})",
        AssetSourceKind.Process => $"process '{DisplayName}'",
        _ => DisplayName,
    };

    private AssetSourceRequest(
        AssetSourceKind kind,
        string? location,
        int? processId,
        string? processName,
        string displayName,
        IReadOnlyDictionary<string, object?>? options)
    {
        Kind = kind;
        Location = location;
        ProcessId = processId;
        ProcessName = processName;
        DisplayName = displayName;
        Options = options is null
            ? EmptyOptions
            : new Dictionary<string, object?>(options, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Creates a file-backed source request.
    /// </summary>
    /// <param name="path">The file path to mount.</param>
    /// <returns>A file-backed source request.</returns>
    public static AssetSourceRequest ForFile(string path) => ForFile(path, null);

    /// <summary>
    /// Creates a file-backed source request.
    /// </summary>
    /// <param name="path">The file path to mount.</param>
    /// <param name="options">Optional per-mount options.</param>
    /// <returns>A file-backed source request.</returns>
    public static AssetSourceRequest ForFile(string path, IReadOnlyDictionary<string, object?>? options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string fullPath = Path.GetFullPath(path);
        return new AssetSourceRequest(
            AssetSourceKind.File,
            fullPath,
            processId: null,
            processName: null,
            Path.GetFileName(fullPath),
            options);
    }

    /// <summary>
    /// Creates a directory-backed source request.
    /// </summary>
    /// <param name="path">The directory path to mount.</param>
    /// <returns>A directory-backed source request.</returns>
    public static AssetSourceRequest ForDirectory(string path) => ForDirectory(path, null);

    /// <summary>
    /// Creates a directory-backed source request.
    /// </summary>
    /// <param name="path">The directory path to mount.</param>
    /// <param name="options">Optional per-mount options.</param>
    /// <returns>A directory-backed source request.</returns>
    public static AssetSourceRequest ForDirectory(string path, IReadOnlyDictionary<string, object?>? options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string fullPath = Path.GetFullPath(path);
        string trimmedPath = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return new AssetSourceRequest(
            AssetSourceKind.Directory,
            fullPath,
            processId: null,
            processName: null,
            Path.GetFileName(trimmedPath),
            options);
    }

    /// <summary>
    /// Creates a process-backed source request using a process identifier.
    /// </summary>
    /// <param name="processId">The target process identifier.</param>
    /// <returns>A process-backed source request.</returns>
    public static AssetSourceRequest ForProcess(int processId) => ForProcess(processId, null);

    /// <summary>
    /// Creates a process-backed source request using a process identifier.
    /// </summary>
    /// <param name="processId">The target process identifier.</param>
    /// <param name="options">Optional per-mount options.</param>
    /// <returns>A process-backed source request.</returns>
    public static AssetSourceRequest ForProcess(int processId, IReadOnlyDictionary<string, object?>? options)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(processId);

        return new AssetSourceRequest(
            AssetSourceKind.Process,
            location: null,
            processId,
            processName: null,
            $"PID {processId}",
            options);
    }

    /// <summary>
    /// Creates a process-backed source request using a process name.
    /// </summary>
    /// <param name="processName">The target process name.</param>
    /// <returns>A process-backed source request.</returns>
    public static AssetSourceRequest ForProcess(string processName) => ForProcess(processName, null);

    /// <summary>
    /// Creates a process-backed source request using a process name.
    /// </summary>
    /// <param name="processName">The target process name.</param>
    /// <param name="options">Optional per-mount options.</param>
    /// <returns>A process-backed source request.</returns>
    public static AssetSourceRequest ForProcess(string processName, IReadOnlyDictionary<string, object?>? options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processName);

        string trimmedName = processName.Trim();
        return new AssetSourceRequest(
            AssetSourceKind.Process,
            location: null,
            processId: null,
            trimmedName,
            trimmedName,
            options);
    }
}
