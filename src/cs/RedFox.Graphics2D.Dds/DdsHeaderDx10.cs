using System.Runtime.InteropServices;

namespace RedFox.Graphics2D.IO
{
    /// <summary>
    /// Represents the DX10 extended header that follows the standard DDS header
    /// when the pixel format FourCC is "DX10".
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct DdsHeaderDx10
    {
        /// <summary>
        /// The DXGI format code describing the pixel layout.
        /// </summary>
        public uint DxgiFormat;

        /// <summary>
        /// The resource dimension (1D, 2D, or 3D texture).
        /// </summary>
        public DdsResourceDimension ResourceDimension;

        /// <summary>
        /// Miscellaneous flags (e.g., cubemap indicator).
        /// </summary>
        public uint MiscFlag;

        /// <summary>
        /// The number of textures in the array (1 for non-array textures).
        /// </summary>
        public uint ArraySize;

        /// <summary>
        /// Additional flags (e.g., alpha mode).
        /// </summary>
        public uint MiscFlags2;
    }
}
