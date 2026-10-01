using System.Diagnostics.CodeAnalysis;

namespace RedFox.GameExtraction;

/// <summary>
/// Represents a single exportable or previewable asset discovered within a mounted source.
/// </summary>
public sealed class Asset
{
    private static readonly Dictionary<string, object?> EmptyMetadata = new(StringComparer.OrdinalIgnoreCase);

    private IAssetHandler? _handler;

    /// <summary>
    /// Gets the mounted source that owns the asset when available.
    /// </summary>
    public IAssetSource? Source { get; private set; }

    /// <summary>
    /// Gets the name of the asset.
    /// </summary>
    /// <remarks>
    /// This may be a simple file name, a relative path, or any identifier the source reader provides.
    /// The asset does not interpret or normalize this value.
    /// </remarks>
    public string Name { get; }

    /// <summary>
    /// Gets the logical asset type used for handler dispatch and display.
    /// </summary>
    public string Type { get; }

    /// <summary>
    /// Gets the broad content category, independently of the source-specific asset type.
    /// </summary>
    public AssetCategory Category { get; }

    /// <summary>
    /// Gets optional secondary information describing the asset.
    /// </summary>
    public string? Information { get; }

    /// <summary>
    /// Gets the source-specific data token used by handlers to access raw asset data.
    /// </summary>
    /// <remarks>
    /// The value depends on the source type — for example a <c>ZipArchiveEntry</c> for ZIP
    /// archives, a pointer value for memory-backed sources, or any custom object the
    /// source reader provides. Handlers cast this to the type they expect.
    /// </remarks>
    public object? DataSource { get; }

    /// <summary>
    /// Gets source-specific metadata associated with the asset.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Metadata { get; }

    /// <summary>
    /// Gets optional loader-defined data that consumers such as custom asset comparers can inspect.
    /// </summary>
    public object? UserData { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Asset"/> class.
    /// </summary>
    /// <param name="name">The name of the asset.</param>
    /// <param name="type">The logical asset type used for handler dispatch and display.</param>
    public Asset(string name, string type)
        : this(name, type, null, null, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Asset"/> class.
    /// </summary>
    /// <param name="name">The name of the asset.</param>
    /// <param name="type">The logical asset type used for handler dispatch and display.</param>
    /// <param name="dataSource">The source-specific data token used by handlers to access raw asset data.</param>
    public Asset(string name, string type, object? dataSource)
        : this(name, type, dataSource, null, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Asset"/> class.
    /// </summary>
    /// <param name="name">The name of the asset.</param>
    /// <param name="type">The logical asset type used for handler dispatch and display.</param>
    /// <param name="dataSource">The source-specific data token used by handlers to access raw asset data.</param>
    /// <param name="information">Optional secondary information describing the asset.</param>
    public Asset(string name, string type, object? dataSource, string? information)
        : this(name, type, dataSource, information, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Asset"/> class.
    /// </summary>
    /// <param name="name">The name of the asset.</param>
    /// <param name="type">The logical asset type used for handler dispatch and display.</param>
    /// <param name="dataSource">The source-specific data token used by handlers to access raw asset data.</param>
    /// <param name="information">Optional secondary information describing the asset.</param>
    /// <param name="metadata">Optional source-specific metadata associated with the asset.</param>
    public Asset(string name, string type, object? dataSource, string? information, IReadOnlyDictionary<string, object?>? metadata)
        : this(name, type, dataSource, information, metadata, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Asset"/> class with optional source metadata and loader-defined user data.
    /// </summary>
    /// <param name="name">The name of the asset.</param>
    /// <param name="type">The logical asset type used for handler dispatch and display.</param>
    /// <param name="dataSource">The source-specific data token used by handlers to access raw asset data.</param>
    /// <param name="information">Optional secondary information describing the asset.</param>
    /// <param name="metadata">Optional source-specific metadata associated with the asset.</param>
    /// <param name="userData">Optional loader-defined data for consumers such as asset comparers.</param>
    public Asset(string name, string type, object? dataSource, string? information, IReadOnlyDictionary<string, object?>? metadata, object? userData)
        : this(name, type, dataSource, information, metadata, userData, AssetCategory.Other)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Asset"/> class with its broad content category.
    /// </summary>
    /// <param name="name">The name of the asset.</param>
    /// <param name="type">The logical asset type used for handler dispatch and display.</param>
    /// <param name="dataSource">The source-specific data token used by handlers to access raw asset data.</param>
    /// <param name="information">Optional secondary information describing the asset.</param>
    /// <param name="metadata">Optional source-specific metadata associated with the asset.</param>
    /// <param name="userData">Optional loader-defined data for consumers such as asset comparers.</param>
    /// <param name="category">The broad content category of the asset.</param>
    public Asset(string name, string type, object? dataSource, string? information, IReadOnlyDictionary<string, object?>? metadata, object? userData, AssetCategory category)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(type);

        Name = name;
        Type = type;
        Category = category;
        DataSource = dataSource;
        Information = information;
        Metadata = metadata is null
            ? EmptyMetadata
            : new Dictionary<string, object?>(metadata, StringComparer.OrdinalIgnoreCase);
        UserData = userData;
    }

    internal void AttachSource(IAssetSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (Source is not null && !ReferenceEquals(Source, source))
        {
            throw new InvalidOperationException("The asset is already attached to a different source.");
        }

        Source = source;
    }

    internal void DetachSource()
    {
        Source = null;
    }

    internal bool TryGetHandler([NotNullWhen(true)] out IAssetHandler? handler)
    {
        handler = _handler;
        return handler is not null;
    }

    internal void SetHandler(IAssetHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _handler ??= handler;
    }

    /// <summary>
    /// Returns the asset's name.
    /// </summary>
    /// <returns>The name of the asset.</returns>
    public override string ToString() => Name;
}
