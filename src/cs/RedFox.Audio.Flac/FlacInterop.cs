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

internal static unsafe partial class FlacInterop
{
    private const string Library = "libFLAC";

    public const int Ok = 0;

    public const int ReadEndOfStream = 1;

    public const int ReadAbort = 2;

    public const int WriteAbort = 1;

    public const int DecoderEndOfStream = 4;

    public const int StreamInfoType = 0;

    [LibraryImport(Library, EntryPoint = "FLAC__stream_decoder_new")]
    public static partial IntPtr DecoderNew();

    [LibraryImport(Library, EntryPoint = "FLAC__stream_decoder_delete")]
    public static partial void DecoderDelete(IntPtr decoder);

    [LibraryImport(Library, EntryPoint = "FLAC__stream_decoder_init_stream")]
    public static partial int DecoderInitStream(IntPtr decoder, delegate* unmanaged<IntPtr, byte*, nuint*, IntPtr, int> read, delegate* unmanaged<IntPtr, ulong, IntPtr, int> seek, delegate* unmanaged<IntPtr, ulong*, IntPtr, int> tell, delegate* unmanaged<IntPtr, ulong*, IntPtr, int> length, delegate* unmanaged<IntPtr, IntPtr, int> eof, delegate* unmanaged<IntPtr, IntPtr, int**, IntPtr, int> write, delegate* unmanaged<IntPtr, IntPtr, IntPtr, void> metadata, delegate* unmanaged<IntPtr, int, IntPtr, void> error, IntPtr clientData);

    [LibraryImport(Library, EntryPoint = "FLAC__stream_decoder_process_single")]
    public static partial int DecoderProcessSingle(IntPtr decoder);

    [LibraryImport(Library, EntryPoint = "FLAC__stream_decoder_seek_absolute")]
    public static partial int DecoderSeekAbsolute(IntPtr decoder, ulong sample);

    [LibraryImport(Library, EntryPoint = "FLAC__stream_decoder_flush")]
    public static partial int DecoderFlush(IntPtr decoder);

    [LibraryImport(Library, EntryPoint = "FLAC__stream_decoder_get_state")]
    public static partial int DecoderGetState(IntPtr decoder);

    [LibraryImport(Library, EntryPoint = "FLAC__stream_decoder_get_resolved_state_string")]
    private static partial IntPtr DecoderGetResolvedStateString(IntPtr decoder);

    [LibraryImport(Library, EntryPoint = "FLAC__stream_encoder_new")]
    public static partial IntPtr EncoderNew();

    [LibraryImport(Library, EntryPoint = "FLAC__stream_encoder_delete")]
    public static partial void EncoderDelete(IntPtr encoder);

    [LibraryImport(Library, EntryPoint = "FLAC__stream_encoder_set_streamable_subset")]
    public static partial int EncoderSetStreamableSubset(IntPtr encoder, int value);

    [LibraryImport(Library, EntryPoint = "FLAC__stream_encoder_set_channels")]
    public static partial int EncoderSetChannels(IntPtr encoder, uint value);

    [LibraryImport(Library, EntryPoint = "FLAC__stream_encoder_set_bits_per_sample")]
    public static partial int EncoderSetBitsPerSample(IntPtr encoder, uint value);

    [LibraryImport(Library, EntryPoint = "FLAC__stream_encoder_set_sample_rate")]
    public static partial int EncoderSetSampleRate(IntPtr encoder, uint value);

    [LibraryImport(Library, EntryPoint = "FLAC__stream_encoder_set_compression_level")]
    public static partial int EncoderSetCompressionLevel(IntPtr encoder, uint value);

    [LibraryImport(Library, EntryPoint = "FLAC__stream_encoder_init_stream")]
    public static partial int EncoderInitStream(IntPtr encoder, delegate* unmanaged<IntPtr, byte*, nuint, uint, uint, IntPtr, int> write, delegate* unmanaged<IntPtr, ulong, IntPtr, int> seek, delegate* unmanaged<IntPtr, ulong*, IntPtr, int> tell, delegate* unmanaged<IntPtr, IntPtr, IntPtr, void> metadata, IntPtr clientData);

    [LibraryImport(Library, EntryPoint = "FLAC__stream_encoder_process_interleaved")]
    public static partial int EncoderProcessInterleaved(IntPtr encoder, int* buffer, uint samples);

    [LibraryImport(Library, EntryPoint = "FLAC__stream_encoder_finish")]
    public static partial int EncoderFinish(IntPtr encoder);

    [LibraryImport(Library, EntryPoint = "FLAC__stream_encoder_get_resolved_state_string")]
    private static partial IntPtr EncoderGetResolvedStateString(IntPtr encoder);

    public static string GetDecoderState(IntPtr decoder) => Marshal.PtrToStringUTF8(DecoderGetResolvedStateString(decoder)) ?? "Unknown FLAC decoder state.";

    public static string GetEncoderState(IntPtr encoder) => Marshal.PtrToStringUTF8(EncoderGetResolvedStateString(encoder)) ?? "Unknown FLAC encoder state.";
}
