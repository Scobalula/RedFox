using System.Text;

namespace RedFox.Graphics2D.IO
{
    /// <summary>
    /// Writes <see cref="Image"/> instances to DDS (DirectDraw Surface) files using a DX10-extended header.
    /// </summary>
    public static class DdsWriter
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
            DdsHeaderFactory.ValidateImage(image);
            DdsHeader header = DdsHeaderFactory.CreateHeader(image);
            DdsHeaderDx10 dx10Header = DdsHeaderFactory.CreateDx10Header(image);

            using BinaryWriter writer = new(stream, Encoding.UTF8, true);

            writer.Write(DdsConstants.Magic);
            DdsStructSerializer.Write(writer, header);
            DdsStructSerializer.Write(writer, dx10Header);
            writer.Write(image.PixelData);
        }
    }
}
