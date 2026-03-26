using static RedFox.Graphics2D.Tiff.TiffConstants;

namespace RedFox.Graphics2D.Tiff
{
    /// <summary>
    /// Represents a single 12-byte IFD (Image File Directory) entry from a TIFF file.
    /// </summary>
    /// <param name="Tag">The tag identifier (e.g., <see cref="TagImageWidth"/>).</param>
    /// <param name="Type">The data type code (e.g., <see cref="TypeShort"/>, <see cref="TypeLong"/>).</param>
    /// <param name="Count">The number of values of the indicated <paramref name="Type"/>.</param>
    /// <param name="ValueOrOffset">
    /// Contains the value directly if it fits in 4 bytes, otherwise the byte offset
    /// to the value data within the TIFF file.
    /// </param>
    public readonly record struct TiffIfdEntry(ushort Tag, ushort Type, uint Count, uint ValueOrOffset);
}
