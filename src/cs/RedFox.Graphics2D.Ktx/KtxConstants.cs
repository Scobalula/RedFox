namespace RedFox.Graphics2D.Ktx
{
    internal static class KtxConstants
    {
        public static ReadOnlySpan<byte> Identifier =>
        [
            0xAB, 0x4B, 0x54, 0x58, 0x20, 0x31, 0x31, 0xBB,
            0x0D, 0x0A, 0x1A, 0x0A,
        ];

        public const uint NativeEndianness = 0x04030201;
        public const uint ReversedEndianness = 0x01020304;

        public const uint GlUnsignedByte = 0x1401;
        public const uint GlHalfFloat = 0x140B;
        public const uint GlFloat = 0x1406;

        public const uint GlRed = 0x1903;
        public const uint GlRgb = 0x1907;
        public const uint GlRgba = 0x1908;
        public const uint GlBgra = 0x80E1;
        public const uint GlRg = 0x8227;

        public const uint GlRgba8 = 0x8058;
        public const uint GlSrgb8Alpha8 = 0x8C43;
        public const uint GlRgba16F = 0x881A;
        public const uint GlRgba32F = 0x8814;

        public const uint GlCompressedRgbS3TcDxt1Ext = 0x83F0;
        public const uint GlCompressedRgbaS3TcDxt1Ext = 0x83F1;
        public const uint GlCompressedRgbaS3TcDxt3Ext = 0x83F2;
        public const uint GlCompressedRgbaS3TcDxt5Ext = 0x83F3;
        public const uint GlCompressedSrgbS3TcDxt1Ext = 0x8C4C;
        public const uint GlCompressedSrgbAlphaS3TcDxt1Ext = 0x8C4D;
        public const uint GlCompressedSrgbAlphaS3TcDxt3Ext = 0x8C4E;
        public const uint GlCompressedSrgbAlphaS3TcDxt5Ext = 0x8C4F;
        public const uint GlCompressedRedRgtc1 = 0x8DBB;
        public const uint GlCompressedSignedRedRgtc1 = 0x8DBC;
        public const uint GlCompressedRgRgtc2 = 0x8DBD;
        public const uint GlCompressedSignedRgRgtc2 = 0x8DBE;
        public const uint GlCompressedRgbaBptcUnorm = 0x8E8C;
        public const uint GlCompressedSrgbAlphaBptcUnorm = 0x8E8D;
        public const uint GlCompressedRgbBptcSignedFloat = 0x8E8E;
        public const uint GlCompressedRgbBptcUnsignedFloat = 0x8E8F;
    }
}
