using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RedFox.Graphics2D.IO
{
    /// <summary>
    /// Inline array of 11 <see cref="uint"/> values representing the reserved region in the DDS header.
    /// </summary>
    [InlineArray(11)]
    [StructLayout(LayoutKind.Sequential)]
    public struct DdsReserved11
    {
        private uint _element;
    }
}
