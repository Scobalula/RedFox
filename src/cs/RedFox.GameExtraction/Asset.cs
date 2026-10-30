using System.Globalization;

namespace RedFox.GameExtraction;

/// <summary>
/// Represents a single exportable or previewable asset discovered within a mounted source.
/// </summary>
public class Asset
{
    private static readonly Dictionary<string, object?> EmptyMetadata = new(StringComparer.OrdinalIgnoreCase);

    private readonly string? _information;

    /// <summary>
    /// Gets the mounted source that owns the asset when available.
    /// </summary>
    public IAssetSource? Owner { get; private set; }

    public IAssetHandler? Handler { get; init; }

    public string? ThumbnailKey { get; init; }

    /// <summary>
    /// Gets the name of the asset.
    /// </summary>
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
    public virtual string? Information => _information;

    /// <summary>
    /// Gets the source-specific value used by handlers to access raw asset data.
    /// </summary>
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
    /// <param name="dataSource">The source-specific value used by handlers to access raw asset data.</param>
    public Asset(string name, string type, object? dataSource)
        : this(name, type, dataSource, null, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Asset"/> class.
    /// </summary>
    /// <param name="name">The name of the asset.</param>
    /// <param name="type">The logical asset type used for handler dispatch and display.</param>
    /// <param name="dataSource">The source-specific value used by handlers to access raw asset data.</param>
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
    /// <param name="dataSource">The source-specific value used by handlers to access raw asset data.</param>
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
    /// <param name="dataSource">The source-specific value used by handlers to access raw asset data.</param>
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
    /// <param name="dataSource">The source-specific value used by handlers to access raw asset data.</param>
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
        _information = information;
        Metadata = metadata is null
            ? EmptyMetadata
            : new Dictionary<string, object?>(metadata, StringComparer.OrdinalIgnoreCase);
        UserData = userData;
    }

    /// <summary>
    /// Gets a numeric value used by search filters, read from <see cref="Metadata"/> by default.
    /// </summary>
    /// <param name="key">The lower-case search key, such as bonecount or width.</param>
    /// <param name="value">The value when the asset exposes the key.</param>
    /// <returns><see langword="true"/> when the asset exposes the key; otherwise, <see langword="false"/>.</returns>
    public virtual bool TryGetSearchValue(string key, out long value)
    {
        if (Metadata.TryGetValue(key, out object? metadata) && metadata is bool or sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal)
        {
            value = Convert.ToInt64(metadata, CultureInfo.InvariantCulture);
            return true;
        }

        value = 0;
        return false;
    }

    /// <summary>
    /// Gets a list of names used by text search filters, such as bone names.
    /// </summary>
    /// <param name="key">The lower-case search key, such as bonename.</param>
    /// <param name="names">The names when the asset exposes the key.</param>
    /// <returns><see langword="true"/> when the asset exposes the key; otherwise, <see langword="false"/>.</returns>
    public virtual bool TryGetSearchNames(string key, out IReadOnlyList<string> names)
    {
        names = [];
        return false;
    }

    /// <summary>
    /// Compares this asset's information with another asset of the same type for sorting.
    /// </summary>
    /// <param name="other">The asset to compare with.</param>
    /// <returns>A negative value when this asset sorts first, zero when equal, or a positive value when it sorts after.</returns>
    public virtual int CompareInformation(Asset other)
    {
        int emptyComparison = string.IsNullOrWhiteSpace(Information).CompareTo(string.IsNullOrWhiteSpace(other.Information));

        if (emptyComparison != 0)
        {
            return emptyComparison;
        }

        return string.Compare(Information, other.Information, CultureInfo.InvariantCulture, CompareOptions.IgnoreCase | CompareOptions.NumericOrdering);
    }

    internal void AttachSource(IAssetSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (Owner is not null && !ReferenceEquals(Owner, source))
        {
            throw new InvalidOperationException("The asset is already attached to a different source.");
        }

        Owner = source;
    }

    internal void DetachSource()
    {
        Owner = null;
    }

    /// <summary>
    /// Returns the asset's name.
    /// </summary>
    /// <returns>The name of the asset.</returns>
    public override string ToString() => Name;
}
