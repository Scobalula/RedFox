namespace RedFox.Imaging.Formats.Tiff;

/// <summary>
/// Represents a single 12-byte IFD (Image File Directory) entry from a TIFF file.
/// </summary>
/// <param name="Tag">The tag identifier (e.g., <see cref="TiffConstants.TagImageWidth"/>).</param>
/// <param name="Type">The data type code (e.g., <see cref="TiffConstants.TypeShort"/>, <see cref="TiffConstants.TypeLong"/>).</param>
/// <param name="Count">The number of values of the indicated <paramref name="Type"/>.</param>
/// <param name="ValueOrOffset">
/// Contains the value directly if it fits in 4 bytes, otherwise the byte offset
/// to the value data within the TIFF file.
/// </param>
internal readonly record struct TiffIfdEntry(ushort Tag, ushort Type, uint Count, uint ValueOrOffset);
