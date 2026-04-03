using RedFox.Graphics2D.Conversion;

namespace RedFox.Graphics2D.BlockCompression.Vulkan;

/// <summary>
/// Groups the source, destination, dimensions, and flags that describe a single BC encode or decode conversion.
/// </summary>
public readonly ref struct VulkanBcConversionRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VulkanBcConversionRequest"/> struct.
    /// </summary>
    /// <param name="source">The source image bytes.</param>
    /// <param name="sourceFormat">The source image format.</param>
    /// <param name="destination">The destination image bytes.</param>
    /// <param name="destinationFormat">The destination image format.</param>
    /// <param name="width">The image width in pixels.</param>
    /// <param name="height">The image height in pixels.</param>
    /// <param name="flags">Optional conversion hints.</param>
    public VulkanBcConversionRequest(
        ReadOnlySpan<byte> source,
        ImageFormat sourceFormat,
        Span<byte> destination,
        ImageFormat destinationFormat,
        int width,
        int height,
        ImageConvertFlags flags)
    {
        Source = source;
        SourceFormat = sourceFormat;
        Destination = destination;
        DestinationFormat = destinationFormat;
        Width = width;
        Height = height;
        Flags = flags;
    }

    /// <summary>
    /// Gets the source image bytes.
    /// </summary>
    public ReadOnlySpan<byte> Source { get; }

    /// <summary>
    /// Gets the source image format.
    /// </summary>
    public ImageFormat SourceFormat { get; }

    /// <summary>
    /// Gets the destination image bytes.
    /// </summary>
    public Span<byte> Destination { get; }

    /// <summary>
    /// Gets the destination image format.
    /// </summary>
    public ImageFormat DestinationFormat { get; }

    /// <summary>
    /// Gets the image width in pixels.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the image height in pixels.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Gets the optional conversion hints.
    /// </summary>
    public ImageConvertFlags Flags { get; }
}
