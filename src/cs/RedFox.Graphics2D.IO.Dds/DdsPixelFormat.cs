using System.Runtime.InteropServices;

namespace RedFox.Graphics2D.IO
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct DdsPixelFormat
    {
        public uint Size;
        public DdsPixelFormatFlags Flags;
        public uint FourCC;
        public uint RgbBitCount;
        public uint RBitMask;
        public uint GBitMask;
        public uint BBitMask;
        public uint ABitMask;
    }
}
