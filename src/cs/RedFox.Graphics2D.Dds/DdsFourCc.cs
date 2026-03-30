using System.Runtime.CompilerServices;

namespace RedFox.Graphics2D.IO
{
    /// <summary>
    /// Well-known DDS FourCC codes used to identify compressed pixel formats in legacy DDS headers.
    /// </summary>
    public static class DdsFourCc
    {
        /// <summary>
        /// FourCC for DXT1 (BC1) compression.
        /// </summary>
        public static readonly uint Dxt1 = Make('D', 'X', 'T', '1');

        /// <summary>
        /// FourCC for DXT2 (premultiplied BC2) compression.
        /// </summary>
        public static readonly uint Dxt2 = Make('D', 'X', 'T', '2');

        /// <summary>
        /// FourCC for DXT3 (BC2) compression.
        /// </summary>
        public static readonly uint Dxt3 = Make('D', 'X', 'T', '3');

        /// <summary>
        /// FourCC for DXT4 (premultiplied BC3) compression.
        /// </summary>
        public static readonly uint Dxt4 = Make('D', 'X', 'T', '4');

        /// <summary>
        /// FourCC for DXT5 (BC3) compression.
        /// </summary>
        public static readonly uint Dxt5 = Make('D', 'X', 'T', '5');

        /// <summary>
        /// FourCC for ATI1 (BC4) compression.
        /// </summary>
        public static readonly uint Ati1 = Make('A', 'T', 'I', '1');

        /// <summary>
        /// FourCC for ATI2 (BC5) compression.
        /// </summary>
        public static readonly uint Ati2 = Make('A', 'T', 'I', '2');

        /// <summary>
        /// FourCC for unsigned BC4 compression.
        /// </summary>
        public static readonly uint Bc4U = Make('B', 'C', '4', 'U');

        /// <summary>
        /// FourCC for signed BC4 compression.
        /// </summary>
        public static readonly uint Bc4S = Make('B', 'C', '4', 'S');

        /// <summary>
        /// FourCC for unsigned BC5 compression.
        /// </summary>
        public static readonly uint Bc5U = Make('B', 'C', '5', 'U');

        /// <summary>
        /// FourCC for signed BC5 compression.
        /// </summary>
        public static readonly uint Bc5S = Make('B', 'C', '5', 'S');

        /// <summary>
        /// FourCC indicating a DX10 extended header follows the standard DDS header.
        /// </summary>
        public static readonly uint Dx10 = Make('D', 'X', '1', '0');

        /// <summary>
        /// Builds a FourCC code from four ASCII characters.
        /// </summary>
        /// <param name="a">First character.</param>
        /// <param name="b">Second character.</param>
        /// <param name="c">Third character.</param>
        /// <param name="d">Fourth character.</param>
        /// <returns>The packed 32-bit FourCC value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Make(char a, char b, char c, char d)
        {
            return (uint)a | ((uint)b << 8) | ((uint)c << 16) | ((uint)d << 24);
        }
    }
}
