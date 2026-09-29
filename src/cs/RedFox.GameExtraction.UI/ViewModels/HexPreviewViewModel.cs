namespace RedFox.GameExtraction.UI.ViewModels;

/// <summary>
/// Presents a raw byte payload to the hex previewer.
/// </summary>
/// <param name="bytes">The payload bytes.</param>
public sealed class HexPreviewViewModel(byte[] bytes)
{
    /// <summary>
    /// Gets the payload bytes.
    /// </summary>
    public byte[] Bytes { get; } = bytes ?? throw new ArgumentNullException(nameof(bytes));
}
