using System.Runtime.InteropServices;

namespace RedFox.Graphics2D.IO
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct DdsHeader
    {
        public uint Size;
        public DdsHeaderFlags Flags;
        public uint Height;
        public uint Width;
        public uint PitchOrLinearSize;
        public uint Depth;
        public uint MipMapCount;
        public DdsReserved11 Reserved1;
        public DdsPixelFormat PixelFormat;
        public DdsCaps Caps;
        public DdsCaps2 Caps2;
        public uint Caps3;
        public uint Caps4;
        public uint Reserved2;
    }
}
