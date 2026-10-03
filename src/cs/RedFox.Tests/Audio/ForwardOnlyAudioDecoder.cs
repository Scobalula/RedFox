using RedFox.Audio;

namespace RedFox.Tests.Audio;

public sealed class ForwardOnlyAudioDecoder(AudioDecoder inner) : AudioDecoder
{
    public override AudioFormat Format => inner.Format;

    public override SampleFormat SampleFormat => inner.SampleFormat;

    public override long FrameCount => inner.FrameCount;

    public override long Position => inner.Position;

    public override int Read(Span<byte> destination) => inner.Read(destination);

    public override void Dispose()
    {
        inner.Dispose();
        base.Dispose();
    }
}
