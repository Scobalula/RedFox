using RedFox.Audio;

namespace RedFox.Tests.Audio;

internal sealed class PacketCollector : IAudioPacketWriter
{
    public MemoryStream Data { get; } = new();

    public long FrameCount { get; private set; }

    public void WritePacket(ReadOnlySpan<byte> packet, int frameCount)
    {
        Data.Write(packet);
        FrameCount += frameCount;
    }
}
