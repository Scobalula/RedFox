using System.Runtime.CompilerServices;
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

    [InlineArray(11)]
    [StructLayout(LayoutKind.Sequential)]
    internal struct DdsReserved11
    {
        private uint _element;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct DdsHeaderDxt10
    {
        public uint DxgiFormat;
        public DdsResourceDimension ResourceDimension;
        public uint MiscFlag;
        public uint ArraySize;
        public uint MiscFlags2;
    }
}
