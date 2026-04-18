// --------------------------------------------------------------------------------------
// RedFox Utility Library - MIT License
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

using System.Runtime.InteropServices;

namespace RedFox.Audio.Flac;

/// <summary>
/// Provides P/Invoke declarations for the native libFLAC library.
/// </summary>
internal unsafe partial class FlacInterop
{
    private const string Library = "Native\\libFLAC";

    #region Decoder

    /// <summary>
    /// Creates a new stream decoder instance.
    /// </summary>
    [LibraryImport(Library, EntryPoint = "FLAC__stream_decoder_new", SetLastError = true)]
    public static partial IntPtr DecoderNew();

    /// <summary>
    /// Frees a decoder instance.
    /// </summary>
    [LibraryImport(Library, EntryPoint = "FLAC__stream_decoder_delete", SetLastError = true)]
    public static partial void DecoderDelete(IntPtr decoder);

    /// <summary>
    /// Initializes a decoder to read FLAC data from a memory buffer.
    /// </summary>
    [LibraryImport(Library, EntryPoint = "FLAC__stream_decoder_init_stream", SetLastError = true)]
    public static partial int DecoderInitStream(
        IntPtr decoder,
        ReadCallback? readCallback,
        SeekCallback? seekCallback,
        TellCallback? tellCallback,
        LengthCallback? lengthCallback,
        EofCallback? eofCallback,
        WriteCallback? writeCallback,
        MetadataCallback? metadataCallback,
        ErrorCallback? errorCallback,
        IntPtr clientData);

    /// <summary>
    /// Initializes a decoder to read FLAC data from Ogg FLAC in a memory buffer.
    /// </summary>
    [LibraryImport(Library, EntryPoint = "FLAC__stream_decoder_init_ogg_stream", SetLastError = true)]
    public static partial int DecoderInitOggStream(
        IntPtr decoder,
        ReadCallback readCallback,
        SeekCallback seekCallback,
        TellCallback tellCallback,
        LengthCallback lengthCallback,
        EofCallback eofCallback,
        WriteCallback writeCallback,
        MetadataCallback metadataCallback,
        ErrorCallback errorCallback,
        IntPtr clientData);

    /// <summary>
    /// Finishes the decoding process and releases encoder resources.
    /// </summary>
    [LibraryImport(Library, EntryPoint = "FLAC__stream_decoder_finish", SetLastError = true)]
    public static partial int DecoderFinish(IntPtr decoder);

    /// <summary>
    /// Processes a single frame of audio.
    /// </summary>
    [LibraryImport(Library, EntryPoint = "FLAC__stream_decoder_process_single", SetLastError = true)]
    public static partial int DecoderProcessSingle(IntPtr decoder);

    /// <summary>
    /// Processes frames until the end of the stream.
    /// </summary>
    [LibraryImport(Library, EntryPoint = "FLAC__stream_decoder_process_until_end_of_stream", SetLastError = true)]
    public static partial int DecoderProcessUntilEndOfStream(IntPtr decoder);

    /// <summary>
    /// Flushes the input and seeks to the given absolute sample number.
    /// </summary>
    [LibraryImport(Library, EntryPoint = "FLAC__stream_decoder_seek_absolute", SetLastError = true)]
    public static partial int DecoderSeekAbsolute(IntPtr decoder, ulong sample);

    /// <summary>
    /// Sets the "md5 signature" checking.
    /// </summary>
    [LibraryImport(Library, EntryPoint = "FLAC__stream_decoder_set_md5_checking", SetLastError = true)]
    public static partial int DecoderSetMd5Checking(IntPtr decoder, int value);

    #endregion

    #region Encoder

    /// <summary>
    /// Creates a new stream encoder instance.
    /// </summary>
    [LibraryImport(Library, EntryPoint = "FLAC__stream_encoder_new", SetLastError = true)]
    public static partial IntPtr EncoderNew();

    /// <summary>
    /// Frees an encoder instance.
    /// </summary>
    [LibraryImport(Library, EntryPoint = "FLAC__stream_encoder_delete", SetLastError = true)]
    public static partial void EncoderDelete(IntPtr encoder);

    /// <summary>
    /// Sets the verify flag.
    /// </summary>
    [LibraryImport(Library, EntryPoint = "FLAC__stream_encoder_set_verify", SetLastError = true)]
    public static partial int EncoderSetVerify(IntPtr encoder, int value);

    /// <summary>
    /// Sets the streamable subset flag.
    /// </summary>
    [LibraryImport(Library, EntryPoint = "FLAC__stream_encoder_set_streamable_subset", SetLastError = true)]
    public static partial int EncoderSetStreamableSubset(IntPtr encoder, int value);

    /// <summary>
    /// Sets the number of channels.
    /// </summary>
    [LibraryImport(Library, EntryPoint = "FLAC__stream_encoder_set_channels", SetLastError = true)]
    public static partial int EncoderSetChannels(IntPtr encoder, uint value);

    /// <summary>
    /// Sets the sample rate.
    /// </summary>
    [LibraryImport(Library, EntryPoint = "FLAC__stream_encoder_set_sample_rate", SetLastError = true)]
    public static partial int EncoderSetSampleRate(IntPtr encoder, uint value);

    /// <summary>
    /// Sets the bits per sample.
    /// </summary>
    [LibraryImport(Library, EntryPoint = "FLAC__stream_encoder_set_bits_per_sample", SetLastError = true)]
    public static partial int EncoderSetBitsPerSample(IntPtr encoder, uint value);

    /// <summary>
    /// Sets the compression level (0-8).
    /// </summary>
    [LibraryImport(Library, EntryPoint = "FLAC__stream_encoder_set_compression_level", SetLastError = true)]
    public static partial int EncoderSetCompressionLevel(IntPtr encoder, uint value);

    /// <summary>
    /// Sets the block size.
    /// </summary>
    [LibraryImport(Library, EntryPoint = "FLAC__stream_encoder_set_blocksize", SetLastError = true)]
    public static partial int EncoderSetBlockSize(IntPtr encoder, uint value);

    /// <summary>
    /// Sets the total number of samples.
    /// </summary>
    [LibraryImport(Library, EntryPoint = "FLAC__stream_encoder_set_total_samples_estimate", SetLastError = true)]
    public static partial int EncoderSetTotalSamplesEstimate(IntPtr encoder, ulong value);

    /// <summary>
    /// Initializes an encoder to write FLAC data using a write callback.
    /// </summary>
    [LibraryImport(Library, EntryPoint = "FLAC__stream_encoder_init_stream", SetLastError = true)]
    public static partial int EncoderInitStream(
        IntPtr encoder,
        EncoderWriteCallback? writeCallback,
        EncoderSeekCallback? seekCallback,
        EncoderTellCallback? tellCallback,
        EncoderMetadataCallback? metadataCallback,
        IntPtr clientData);

    /// <summary>
    /// Initializes an Ogg FLAC encoder.
    /// </summary>
    [LibraryImport(Library, EntryPoint = "FLAC__stream_encoder_init_ogg_stream", SetLastError = true)]
    public static partial int EncoderInitOggStream(
        IntPtr encoder,
        EncoderWriteCallback writeCallback,
        EncoderSeekCallback seekCallback,
        EncoderTellCallback tellCallback,
        EncoderMetadataCallback metadataCallback,
        IntPtr clientData);

    /// <summary>
    /// Finishes the encoding process and releases encoder resources.
    /// </summary>
    [LibraryImport(Library, EntryPoint = "FLAC__stream_encoder_finish", SetLastError = true)]
    public static partial int EncoderFinish(IntPtr encoder);

    /// <summary>
    /// Encodes the block of interleaved samples.
    /// </summary>
    [LibraryImport(Library, EntryPoint = "FLAC__stream_encoder_process_interleaved", SetLastError = true)]
    public static partial int EncoderProcessInterleaved(IntPtr encoder, IntPtr buffer, uint samples);

    #endregion

    #region Callbacks

    /// <summary>
    /// Callback for reading encoded data.
    /// </summary>
    public unsafe delegate ReadCallbackStatus ReadCallback(
        IntPtr decoder,
        byte* buffer,
        int* bytes,
        IntPtr clientData);

    /// <summary>
    /// Callback for seeking within the stream.
    /// </summary>
    public unsafe delegate SeekCallbackStatus SeekCallback(
        IntPtr decoder,
        ulong absoluteByteOffset,
        IntPtr clientData);

    /// <summary>
    /// Callback for getting the current position.
    /// </summary>
    public unsafe delegate TellCallbackStatus TellCallback(
        IntPtr decoder,
        ulong* absoluteByteOffset,
        IntPtr clientData);

    /// <summary>
    /// Callback for getting the stream length.
    /// </summary>
    public unsafe delegate LengthCallbackStatus LengthCallback(
        IntPtr decoder,
        ulong* streamLength,
        IntPtr clientData);

    /// <summary>
    /// Callback for checking end of file.
    /// </summary>
    public unsafe delegate int EofCallback(
        IntPtr decoder,
        IntPtr clientData);

    /// <summary>
    /// Callback for writing decoded PCM data.
    /// </summary>
    public unsafe delegate int WriteCallback(
        IntPtr decoder,
        IntPtr* frame,
        IntPtr buffer,
        IntPtr clientData);

    /// <summary>
    /// Callback for receiving metadata.
    /// </summary>
    public unsafe delegate void MetadataCallback(
        IntPtr decoder,
        IntPtr metadata,
        IntPtr clientData);

    /// <summary>
    /// Callback for error reporting.
    /// </summary>
    public unsafe delegate void ErrorCallback(
        IntPtr decoder,
        int status,
        IntPtr clientData);

    /// <summary>
    /// Callback for writing encoded data.
    /// </summary>
    public unsafe delegate EncoderWriteStatus EncoderWriteCallback(
        IntPtr encoder,
        byte* buffer,
        int bytes,
        int samples,
        int currentFrame,
        IntPtr clientData);

    /// <summary>
    /// Callback for seeking in the output stream.
    /// </summary>
    public unsafe delegate EncoderSeekStatus EncoderSeekCallback(
        IntPtr encoder,
        ulong absoluteByteOffset,
        IntPtr clientData);

    /// <summary>
    /// Callback for getting the output position.
    /// </summary>
    public unsafe delegate EncoderTellStatus EncoderTellCallback(
        IntPtr encoder,
        ulong* absoluteByteOffset,
        IntPtr clientData);

    /// <summary>
    /// Callback for metadata encoding.
    /// </summary>
    public unsafe delegate void EncoderMetadataCallback(
        IntPtr encoder,
        IntPtr metadata,
        IntPtr clientData);

    #endregion

    #region Enums

    /// <summary>
    /// Status codes for the read callback.
    /// </summary>
    public enum ReadCallbackStatus : int
    {
        ReadContinue = 0,
        ReadAbort = 1,
    }

    /// <summary>
    /// Status codes for the seek callback.
    /// </summary>
    public enum SeekCallbackStatus : int
    {
        SeekOk = 0,
        SeekError = 1,
        SeekUnsupported = 2,
    }

    /// <summary>
    /// Status codes for the tell callback.
    /// </summary>
    public enum TellCallbackStatus : int
    {
        TellOk = 0,
        TellError = 1,
        TellUnsupported = 2,
    }

    /// <summary>
    /// Status codes for the length callback.
    /// </summary>
    public enum LengthCallbackStatus : int
    {
        LengthOk = 0,
        LengthError = 1,
        LengthUnsupported = 2,
    }

    /// <summary>
    /// Status codes for the encoder write callback.
    /// </summary>
    public enum EncoderWriteStatus : int
    {
        WriteOk = 0,
        WriteFatalError = 1,
    }

    /// <summary>
    /// Status codes for the encoder seek callback.
    /// </summary>
    public enum EncoderSeekStatus : int
    {
        SeekOk = 0,
        SeekError = 1,
        SeekUnsupported = 2,
    }

    /// <summary>
    /// Status codes for the encoder tell callback.
    /// </summary>
    public enum EncoderTellStatus : int
    {
        TellOk = 0,
        TellError = 1,
        TellUnsupported = 2,
    }

    #endregion
}
