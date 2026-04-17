// --------------------------------------------------------------------------------------
// RedFox Utility Library - MIT License
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

using System.Runtime.InteropServices;

namespace RedFox.Audio.Opus;

internal partial class OpusInterop
{
    private const string Library = "opus";

    [LibraryImport(Library, EntryPoint = "opus_encoder_create", SetLastError = true)]
    public static partial IntPtr EncoderCreate(int sampleRate, int channels, int application, out int error);

    [LibraryImport(Library, EntryPoint = "opus_encoder_destroy", SetLastError = true)]
    public static partial void EncoderDestroy(IntPtr encoder);

    [LibraryImport(Library, EntryPoint = "opus_encode", SetLastError = true)]
    public static partial int Encode(IntPtr encoder, ReadOnlySpan<short> pcm, int frameSize, Span<byte> data, int maxDataBytes);

    [LibraryImport(Library, EntryPoint = "opus_decoder_create", SetLastError = true)]
    public static partial IntPtr DecoderCreate(int sampleRate, int channels, out int error);

    [LibraryImport(Library, EntryPoint = "opus_decoder_destroy", SetLastError = true)]
    public static partial void DecoderDestroy(IntPtr decoder);

    [LibraryImport(Library, EntryPoint = "opus_decode", SetLastError = true)]
    public static partial int Decode(IntPtr decoder, ReadOnlySpan<byte> data, int length, Span<short> pcm, int frameSize, int decodeFec);

    [LibraryImport(Library, EntryPoint = "opus_encoder_ctl", SetLastError = true)]
    public static partial int EncoderCtlSet(IntPtr encoder, int request, int value);

    [LibraryImport(Library, EntryPoint = "opus_encoder_ctl", SetLastError = true)]
    public static partial int EncoderCtlGetInt(IntPtr encoder, int request, out int value);

    [LibraryImport(Library, EntryPoint = "opus_decoder_ctl", SetLastError = true)]
    public static partial int DecoderCtlSet(IntPtr decoder, int request, int value);

    [LibraryImport(Library, EntryPoint = "opus_decoder_ctl", SetLastError = true)]
    public static partial int DecoderCtlGetInt(IntPtr decoder, int request, out int value);

    [LibraryImport(Library, EntryPoint = "opus_packet_get_nb_samples", SetLastError = true)]
    public static partial int PacketGetNbSamples(ReadOnlySpan<byte> data, int length, int sampleRate);

    [LibraryImport(Library, EntryPoint = "opus_strerror", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    public static partial string StrError(int error);

    public const int Ok = 0;

    public const int SetBitrateRequest = 4002;
    public const int GetBitrateRequest = 4003;
    public const int SetComplexityRequest = 4010;
    public const int GetComplexityRequest = 4011;
    public const int SetVbrRequest = 4006;
    public const int GetVbrRequest = 4007;
    public const int SetBandwidthRequest = 4008;
    public const int GetBandwidthRequest = 4009;
    public const int SetDtxRequest = 4016;
    public const int GetDtxRequest = 4017;
    public const int SetPacketLossPercRequest = 4014;
    public const int GetPacketLossPercRequest = 4015;
    public const int GetLookaheadRequest = 4027;
    public const int GetLastPacketDurationRequest = 4039;
}
