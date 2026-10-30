namespace RedFox.GameExtraction;

/// <summary>
/// Represents a sound asset whose length and format can be sorted and searched.
/// </summary>
public class SoundAsset : Asset
{
    /// <summary>
    /// Gets the playback length of the sound.
    /// </summary>
    public TimeSpan Length { get; init; }

    /// <summary>
    /// Gets the number of samples per second.
    /// </summary>
    public int SampleRate { get; init; }

    /// <summary>
    /// Gets the number of channels.
    /// </summary>
    public int Channels { get; init; }

    /// <summary>
    /// Gets the number of bits per sample.
    /// </summary>
    public int BitsPerSample { get; init; }

    /// <inheritdoc/>
    public override string? Information => $"Length: {Length:mm\\:ss\\.fff}, Rate: {SampleRate}, Channels: {Channels}, Bits: {BitsPerSample}";

    /// <summary>
    /// Initializes a new instance of the <see cref="SoundAsset"/> class.
    /// </summary>
    /// <param name="name">The name of the asset.</param>
    /// <param name="type">The logical asset type used for handler dispatch and display.</param>
    /// <param name="dataSource">The source-specific value used by handlers to access raw asset data.</param>
    public SoundAsset(string name, string type, object? dataSource) : this(name, type, dataSource, null, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SoundAsset"/> class with source metadata and loader-defined user data.
    /// </summary>
    /// <param name="name">The name of the asset.</param>
    /// <param name="type">The logical asset type used for handler dispatch and display.</param>
    /// <param name="dataSource">The source-specific value used by handlers to access raw asset data.</param>
    /// <param name="metadata">Optional source-specific metadata associated with the asset.</param>
    /// <param name="userData">Optional loader-defined data for consumers such as asset comparers.</param>
    public SoundAsset(string name, string type, object? dataSource, IReadOnlyDictionary<string, object?>? metadata, object? userData) : base(name, type, dataSource, null, metadata, userData, AssetCategory.Sound)
    {
    }

    /// <inheritdoc/>
    public override bool TryGetSearchValue(string key, out long value)
    {
        switch (key)
        {
            case "length":
                value = (long)Length.TotalMilliseconds;
                return true;
            case "samplerate":
                value = SampleRate;
                return true;
            case "channels":
                value = Channels;
                return true;
            case "bits":
                value = BitsPerSample;
                return true;
            default:
                return base.TryGetSearchValue(key, out value);
        }
    }

    /// <inheritdoc/>
    public override int CompareInformation(Asset other)
    {
        if (other is not SoundAsset sound)
        {
            return base.CompareInformation(other);
        }

        return Length.CompareTo(sound.Length);
    }
}
