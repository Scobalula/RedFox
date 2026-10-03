using System.Runtime.InteropServices;
using RedFox.Audio;

namespace RedFox.GameExtraction.UI;

internal static class WaveformPeaks
{
    private const int BlockFrames = 1024;

    public static (float Min, float Max)[][] Build(AudioClip clip, int resolution, CancellationToken cancellationToken)
    {
        using AudioDecoder decoder = clip.OpenDecoder();
        int channels = decoder.Format.Channels;
        int bytesPerFrame = decoder.BytesPerFrame;
        byte[] block = new byte[BlockFrames * bytesPerFrame];
        float[] samples = new float[BlockFrames * channels];
        List<(float Min, float Max)>[] blocks = [.. Enumerable.Range(0, channels).Select(_ => new List<(float Min, float Max)>())];
        int frames;

        while ((frames = decoder.Read(block)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SampleConverter.Convert(block.AsSpan(0, frames * bytesPerFrame), decoder.SampleFormat, MemoryMarshal.AsBytes(samples.AsSpan()), SampleFormat.Float32);

            for (int channel = 0; channel < channels; channel++)
            {
                float min = 0.0f;
                float max = 0.0f;

                for (int frame = 0; frame < frames; frame++)
                {
                    float value = samples[(frame * channels) + channel];
                    min = Math.Min(min, value);
                    max = Math.Max(max, value);
                }

                blocks[channel].Add((min, max));
            }
        }

        return [.. blocks.Select(channel => Reduce(CollectionsMarshal.AsSpan(channel), Math.Min(resolution, channel.Count)))];
    }

    public static (float Min, float Max)[] Reduce(ReadOnlySpan<(float Min, float Max)> peaks, int width)
    {
        (float Min, float Max)[] reduced = new (float, float)[width];

        for (int x = 0; x < width; x++)
        {
            int start = (int)((long)x * peaks.Length / width);
            int end = Math.Min(peaks.Length, Math.Max(start + 1, (int)((long)(x + 1) * peaks.Length / width)));
            float min = 0.0f;
            float max = 0.0f;

            for (int index = start; index < end; index++)
            {
                min = Math.Min(min, peaks[index].Min);
                max = Math.Max(max, peaks[index].Max);
            }

            reduced[x] = (min, max);
        }

        return reduced;
    }
}
