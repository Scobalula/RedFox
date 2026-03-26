using System.Runtime.InteropServices;

namespace RedFox.Graphics2D.IO
{
    /// <summary>
    /// Represents the 124-byte DDS file header, stored immediately after the magic number.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct DdsHeader
    {
        /// <summary>
        /// Size of this structure in bytes (must be 124).
        /// </summary>
        public uint Size;

        /// <summary>
        /// Flags indicating which header fields contain valid data.
        /// </summary>
        public DdsHeaderFlags Flags;

        /// <summary>
        /// Surface height in pixels.
        /// </summary>
        public uint Height;

        /// <summary>
        /// Surface width in pixels.
        /// </summary>
        public uint Width;

        /// <summary>
        /// Row pitch (for uncompressed) or total byte size (for compressed) of the top-level surface.
        /// </summary>
        public uint PitchOrLinearSize;

        /// <summary>
        /// Depth of a volume texture in pixels; otherwise unused.
        /// </summary>
        public uint Depth;

        /// <summary>
        /// Number of mipmap levels (1 if the surface has no mipmaps).
        /// </summary>
        public uint MipMapCount;

        /// <summary>
        /// Reserved; not used.
        /// </summary>
        public DdsReserved11 Reserved1;

        /// <summary>
        /// The pixel format description.
        /// </summary>
        public DdsPixelFormat PixelFormat;

        /// <summary>
        /// Surface capability flags.
        /// </summary>
        public DdsCaps Caps;

        /// <summary>
        /// Extended surface capability flags for cubemaps and volume textures.
        /// </summary>
        public DdsCaps2 Caps2;

        /// <summary>
        /// Unused capability field.
        /// </summary>
        public uint Caps3;

        /// <summary>
        /// Unused capability field.
        /// </summary>
        public uint Caps4;

        /// <summary>
        /// Reserved; not used.
        /// </summary>
        public uint Reserved2;
    }
}
