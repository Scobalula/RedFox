using System.Text;

namespace RedFox.Graphics2D.IO
{
    /// <summary>
    /// Writes <see cref="Image"/> instances to DDS (DirectDraw Surface) files using a DX10-extended header.
    /// </summary>
    public static class DDSWriter
    {
        /// <summary>
        /// Writes an image to the specified file path.
        /// </summary>
        /// <param name="filePath">The destination DDS file path.</param>
        /// <param name="image">The source image.</param>
        public static void Save(string filePath, Image image)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            using FileStream stream = new(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            Save(stream, image);
        }

        /// <summary>
        /// Writes an image to the supplied writable stream.
        /// </summary>
        /// <param name="stream">The destination stream.</param>
        /// <param name="image">The source image.</param>
        public static void Save(Stream stream, Image image)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentNullException.ThrowIfNull(image);
            if (!stream.CanWrite) { throw new IOException("The supplied stream is not writable."); }

            DdsHeaderFactory.ValidateImage(image);
            DdsHeader header = DdsHeaderFactory.CreateHeader(image);
            DdsHeaderDxt10 dxt10 = DdsHeaderFactory.CreateDxt10Header(image);

            using BinaryWriter writer = new(stream, Encoding.UTF8, true);
            writer.Write(DdsConstants.Magic);
            DdsStructSerializer.Write(writer, header);
            DdsStructSerializer.Write(writer, dxt10);
            writer.Write((ReadOnlySpan<byte>)image.PixelData);
        }
    }
}
