using System.Runtime.InteropServices;

namespace RedFox.Audio.Wwise;
[StructLayout(LayoutKind.Sequential)]

internal struct OggPacket
{
    public nint Data;

    public int Length;

    public int BeginningOfStream;

    public int EndOfStream;

    public long GranulePosition;

    public long PacketNumber;
}
