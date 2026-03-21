using System.Runtime.InteropServices;

namespace RedFox.Graphics2D.IO
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct DdsHeaderDx10
    {
        public uint DxgiFormat;
        public DdsResourceDimension ResourceDimension;
        public uint MiscFlag;
        public uint ArraySize;
        public uint MiscFlags2;
    }
}
