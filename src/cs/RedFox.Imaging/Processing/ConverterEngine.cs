using System;
using RedFox.Imaging.Primitives;

namespace RedFox.Imaging.Processing;

/// <summary>
/// Base type for pluggable conversion engines used by <see cref="Image.Convert(ImageFormat, ImageConvertFlags, ConverterEngine?)"/>.
/// </summary>
public abstract class ConverterEngine
{
    /// <summary>
    /// Gets a converter engine instance that performs no custom conversion.
    /// </summary>
    public static ConverterEngine None { get; } = new NoOpConverterEngine();

    /// <summary>
    /// Gets a human-readable engine name.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Attempts to convert a single 2D slice from one image format to another.
    /// Return <see langword="true"/> only when conversion succeeds and destination contains valid output.
    /// </summary>
    /// <param name="source">The source slice data in <paramref name="sourceFormat"/>.</param>
    /// <param name="sourceFormat">The format of <paramref name="source"/>.</param>
    /// <param name="destination">The destination slice buffer in <paramref name="destinationFormat"/>.</param>
    /// <param name="destinationFormat">The format to write to <paramref name="destination"/>.</param>
    /// <param name="width">The slice width in pixels.</param>
    /// <param name="height">The slice height in pixels.</param>
    /// <param name="flags">Conversion hints supplied by the caller.</param>
    /// <returns><c>true</c> when the conversion succeeds; otherwise, <c>false</c>.</returns>
    public abstract bool TryConvert(ReadOnlySpan<byte> source, ImageFormat sourceFormat, Span<byte> destination, ImageFormat destinationFormat, int width, int height, ImageConvertFlags flags);
}
