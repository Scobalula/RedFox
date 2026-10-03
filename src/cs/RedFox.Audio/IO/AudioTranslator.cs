// --------------------------------------------------------------------------------------
// RedFox Utility Library - MIT License
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

namespace RedFox.Audio.IO;

/// <summary>
/// Base class for audio file format translators that read and write audio files.
/// </summary>
public abstract class AudioTranslator
{
    private const int ChunkFrames = 4096;

    /// <summary>
    /// Gets the human-readable name of this translator.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Gets a value indicating whether this translator supports reading audio files.
    /// </summary>
    public abstract bool CanRead { get; }

    /// <summary>
    /// Gets a value indicating whether this translator supports writing audio files.
    /// </summary>
    public abstract bool CanWrite { get; }

    /// <summary>
    /// Gets the file extensions this translator handles (without leading dot).
    /// </summary>
    public abstract IReadOnlyList<string> Extensions { get; }

    /// <summary>
    /// Reads an audio file from the specified file path.
    /// </summary>
    /// <param name="filePath">The path to the audio file.</param>
    /// <returns>An <see cref="AudioClip"/> containing the audio data.</returns>
    public virtual AudioClip Read(string filePath)
    {
        using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Read(stream);
    }

    /// <summary>
    /// Reads an audio stream.
    /// </summary>
    /// <param name="stream">The stream containing audio data.</param>
    /// <returns>An <see cref="AudioClip"/> containing the audio data.</returns>
    public abstract AudioClip Read(Stream stream);

    /// <summary>
    /// Writes an audio clip to a file using default encoding options.
    /// </summary>
    /// <param name="filePath">The path where the audio file will be written.</param>
    /// <param name="clip">The audio clip to write.</param>
    public void Write(string filePath, AudioClip clip) => Write(filePath, clip, new AudioTranslatorOptions());

    /// <summary>
    /// Writes an audio clip to a file with the specified options.
    /// </summary>
    /// <param name="filePath">The path where the audio file will be written.</param>
    /// <param name="clip">The audio clip to write.</param>
    /// <param name="options">The encoding options.</param>
    public void Write(string filePath, AudioClip clip, AudioTranslatorOptions options)
    {
        using FileStream stream = new(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        Write(stream, clip, options);
    }

    /// <summary>
    /// Writes an audio clip to a stream using default encoding options.
    /// </summary>
    /// <param name="stream">The stream where the audio data will be written.</param>
    /// <param name="clip">The audio clip to write.</param>
    public void Write(Stream stream, AudioClip clip) => Write(stream, clip, new AudioTranslatorOptions());

    /// <summary>
    /// Writes an audio clip to a stream with the specified options.
    /// </summary>
    /// <param name="stream">The stream where the audio data will be written.</param>
    /// <param name="clip">The audio clip to write.</param>
    /// <param name="options">The encoding options.</param>
    public abstract void Write(Stream stream, AudioClip clip, AudioTranslatorOptions options);

    /// <summary>
    /// Validates whether this translator can handle the specified file.
    /// </summary>
    /// <param name="filePath">The file path to validate.</param>
    /// <param name="extension">The file extension to validate.</param>
    /// <returns><see langword="true"/> if this translator can handle the file; otherwise <see langword="false"/>.</returns>
    public virtual bool IsValid(string filePath, string extension) => Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Validates whether this translator can handle the specified file by examining its contents.
    /// </summary>
    /// <param name="header">The first bytes of the file.</param>
    /// <param name="filePath">The file path being validated.</param>
    /// <param name="extension">The file extension being validated.</param>
    /// <returns><see langword="true"/> if this translator can handle the file; otherwise <see langword="false"/>.</returns>
    public virtual bool IsValid(ReadOnlySpan<byte> header, string filePath, string extension) => IsValid(filePath, extension);

    /// <summary>
    /// Determines whether an encoded clip can be written without decoding and re-encoding it.
    /// </summary>
    /// <param name="clip">The clip to inspect.</param>
    /// <param name="options">The requested output options.</param>
    /// <returns><see langword="true"/> when the encoded data matches the requested options.</returns>
    protected static bool CanPassthrough(AudioClip clip, AudioTranslatorOptions options) => clip.Encoded is { } encoded && options.SampleFormat is null && (options.Codec is null || options.Codec.Id == encoded.Codec.Id);

    /// <summary>
    /// Copies decoded samples to an output writer, converting them to the requested sample format.
    /// </summary>
    /// <param name="decoder">The decoder providing audio samples.</param>
    /// <param name="format">The sample format required by the output.</param>
    /// <param name="output">The writer receiving the samples.</param>
    protected static void CopySamples(AudioDecoder decoder, SampleFormat format, IAudioPacketWriter output) => Transfer(decoder, format, null, output);

    /// <summary>
    /// Encodes decoded samples and writes the resulting packets to an output writer.</summary>
    /// <param name="decoder">The decoder providing audio samples.</param>
    /// <param name="encoder">The encoder used to produce output packets.</param>
    /// <param name="output">The writer receiving the encoded packets.</param>
    protected static void EncodeSamples(AudioDecoder decoder, AudioEncoder encoder, IAudioPacketWriter output)
    {
        Transfer(decoder, encoder.InputSampleFormat, encoder, output);
        encoder.Complete(output);
    }

    private static void Transfer(AudioDecoder decoder, SampleFormat format, AudioEncoder? encoder, IAudioPacketWriter output)
    {
        int sourceFrameSize = decoder.BytesPerFrame;
        int targetFrameSize = SampleFormatInfo.GetBytesPerSample(format) * decoder.Format.Channels;
        byte[] source = new byte[ChunkFrames * sourceFrameSize];
        byte[] target = decoder.SampleFormat == format ? source : new byte[ChunkFrames * targetFrameSize];
        int frames;

        while ((frames = decoder.Read(source)) > 0)
        {
            if (target != source)
                SampleConverter.Convert(source.AsSpan(0, frames * sourceFrameSize), decoder.SampleFormat, target, format);

            ReadOnlySpan<byte> chunk = target.AsSpan(0, frames * targetFrameSize);

            if (encoder is null)
                output.WritePacket(chunk, frames);
            else
                encoder.Encode(chunk, output);
        }
    }
}
