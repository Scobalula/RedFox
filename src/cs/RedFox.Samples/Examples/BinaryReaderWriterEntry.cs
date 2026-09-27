using System.Runtime.InteropServices;

namespace RedFox.Samples.Examples;

[StructLayout(LayoutKind.Sequential)]
internal struct BinaryReaderWriterEntry
{
    public int Id;
    public short Score;
}
