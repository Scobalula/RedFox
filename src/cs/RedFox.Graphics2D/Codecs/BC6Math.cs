using System;
using System.Runtime.CompilerServices;

namespace RedFox.Graphics2D.Codecs
{
    internal static class BC6Math
    {
        private static int DetermineMode(ulong lo, out int modeBits)
        {
            int low2 = (int)(lo & 3);
            if (low2 < 2)
            {
                modeBits = 2;
                return low2; // modes 0, 1
            }

            // 5-bit mode
            int low5 = (int)(lo & 0x1F);
            modeBits = 5;

            return low5 switch
            {
                0b00010 => 2,
                0b00110 => 3,
                0b01010 => 4,
                0b01110 => 5,
                0b10010 => 6,
                0b10110 => 7,
                0b11010 => 8,
                0b11110 => 9,
                0b00011 => 10,
                0b00111 => 11,
                0b01011 => 12,
                0b01111 => 13,
                _ => -1, // reserved
            };
        }

        // Expose DetermineMode publicly for BC6HCodec to call
        internal static int GetModeFromLowBits(ulong lo, out int modeBits) => DetermineMode(lo, out modeBits);
    }
}
