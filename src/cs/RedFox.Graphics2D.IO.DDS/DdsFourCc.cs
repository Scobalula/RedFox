using System.Runtime.CompilerServices;

namespace RedFox.Graphics2D.IO
{
    internal static class DdsFourCc
    {
        internal static readonly uint Dxt1 = Make('D', 'X', 'T', '1');
        internal static readonly uint Dxt2 = Make('D', 'X', 'T', '2');
        internal static readonly uint Dxt3 = Make('D', 'X', 'T', '3');
        internal static readonly uint Dxt4 = Make('D', 'X', 'T', '4');
        internal static readonly uint Dxt5 = Make('D', 'X', 'T', '5');
        internal static readonly uint Ati1 = Make('A', 'T', 'I', '1');
        internal static readonly uint Ati2 = Make('A', 'T', 'I', '2');
        internal static readonly uint Bc4U = Make('B', 'C', '4', 'U');
        internal static readonly uint Bc4S = Make('B', 'C', '4', 'S');
        internal static readonly uint Bc5U = Make('B', 'C', '5', 'U');
        internal static readonly uint Bc5S = Make('B', 'C', '5', 'S');
        internal static readonly uint Dx10 = Make('D', 'X', '1', '0');

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint Make(char a, char b, char c, char d)
        {
            return (uint)a | ((uint)b << 8) | ((uint)c << 16) | ((uint)d << 24);
        }
    }
}
