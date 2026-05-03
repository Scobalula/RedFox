using RedFox.GameExtraction;

namespace RedFox.GameExtraction.UI.Models;

/// <summary>
/// Presents a registered source reader that can open a process request.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ProcessReaderViewModel"/> class.
/// </remarks>
/// <param name="reader">The source reader represented by this row.</param>
public sealed class ProcessReaderViewModel(IAssetSourceReader reader)
{

    /// <summary>
    /// Gets the source reader.
    /// </summary>
    public IAssetSourceReader Reader { get; } = reader ?? throw new ArgumentNullException(nameof(reader));

    /// <summary>
    /// Gets a display name for the reader.
    /// </summary>
    public string DisplayName => Reader.GetType().Name;
}
