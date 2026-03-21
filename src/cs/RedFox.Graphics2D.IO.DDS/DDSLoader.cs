namespace RedFox.Graphics2D.IO
{
    /// <summary>
    /// Loads DDS (DirectDraw Surface) files into <see cref="Image"/> instances.
    /// Supports legacy headers and DX10-extended headers.
    /// </summary>
    public static class DdsLoader
    {
        /// <summary>
        /// Loads a DDS file from the specified file path.
        /// </summary>
        /// <param name="filePath">The path to the DDS file.</param>
        /// <returns>An <see cref="Image"/> containing the decoded DDS metadata and payload.</returns>
        public static Image Load(string filePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            byte[] data = File.ReadAllBytes(filePath);
            return Load(data);
        }

        /// <summary>
        /// Loads a DDS file from a readable stream.
        /// </summary>
        /// <param name="stream">The stream containing the DDS data.</param>
        /// <returns>An <see cref="Image"/> containing the decoded DDS metadata and payload.</returns>
        public static Image Load(Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);
            if (!stream.CanRead) { throw new IOException("The supplied stream is not readable."); }

            using MemoryStream memoryStream = new();
            stream.CopyTo(memoryStream);
            return Load(memoryStream.ToArray());
        }

        /// <summary>
        /// Loads a DDS file from a byte array.
        /// </summary>
        /// <param name="data">The raw DDS bytes.</param>
        /// <returns>An <see cref="Image"/> containing the decoded DDS metadata and payload.</returns>
        public static Image Load(byte[] data)
        {
            ArgumentNullException.ThrowIfNull(data);
            return Load(data.AsSpan());
        }

        /// <summary>
        /// Loads a DDS file from a read-only span of bytes.
        /// </summary>
        /// <param name="data">The raw DDS bytes.</param>
        /// <returns>An <see cref="Image"/> containing the decoded DDS metadata and payload.</returns>
        public static Image Load(ReadOnlySpan<byte> data)
        {
            DdsMetadata metadata = DdsMetadataReader.Read(data);
            byte[] pixelData = data[metadata.DataOffset..].ToArray();
            return new Image(metadata.Width, metadata.Height, metadata.Depth, metadata.ArraySize, metadata.MipLevels, metadata.Format, metadata.IsCubemap, pixelData);
        }
    }
}
