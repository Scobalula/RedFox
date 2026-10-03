// --------------------------------------------------------------------------------------
// RedFox Utility Library - MIT License
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

namespace RedFox.Audio;

/// <summary>
/// Specifies the spatial arrangement of audio channels in a surround sound format.
/// This is a flags enum allowing combinations of individual channel positions.
/// </summary>
[Flags]
public enum ChannelLayout : uint
{
    /// <summary>
    /// No channels specified.
    /// </summary>
    None = 0,

    /// <summary>
    /// Front left channel.
    /// </summary>
    FrontLeft = 0x1,

    /// <summary>
    /// Front right channel.
    /// </summary>
    FrontRight = 0x2,

    /// <summary>
    /// Front center channel.
    /// </summary>
    FrontCenter = 0x4,

    /// <summary>
    /// Low frequency effects (subwoofer) channel.
    /// </summary>
    LowFrequency = 0x8,

    /// <summary>
    /// Back left channel.
    /// </summary>
    BackLeft = 0x10,

    /// <summary>
    /// Back right channel.
    /// </summary>
    BackRight = 0x20,

    /// <summary>
    /// Front left of center channel.
    /// </summary>
    FrontLeftOfCenter = 0x40,

    /// <summary>
    /// Front right of center channel.
    /// </summary>
    FrontRightOfCenter = 0x80,

    /// <summary>
    /// Back center channel.
    /// </summary>
    BackCenter = 0x100,

    /// <summary>
    /// Side left channel.
    /// </summary>
    SideLeft = 0x200,

    /// <summary>
    /// Side right channel.
    /// </summary>
    SideRight = 0x400,

    /// <summary>
    /// Top center channel.
    /// </summary>
    TopCenter = 0x800,

    /// <summary>
    /// Top front left channel.
    /// </summary>
    TopFrontLeft = 0x1000,

    /// <summary>
    /// Top front center channel.
    /// </summary>
    TopFrontCenter = 0x2000,

    /// <summary>
    /// Top front right channel.
    /// </summary>
    TopFrontRight = 0x4000,

    /// <summary>
    /// Top back left channel.
    /// </summary>
    TopBackLeft = 0x8000,

    /// <summary>
    /// Top back center channel.
    /// </summary>
    TopBackCenter = 0x10000,

    /// <summary>
    /// Top back right channel.
    /// </summary>
    TopBackRight = 0x20000,

    /// <summary>
    /// Mono (single channel) layout.
    /// </summary>
    Mono = FrontCenter,

    /// <summary>
    /// Stereo (two-channel) layout: front left and front right.
    /// </summary>
    Stereo = FrontLeft | FrontRight,

    /// <summary>
    /// Surround layout: front left, front right, and front center.
    /// </summary>
    Surround = FrontLeft | FrontRight | FrontCenter,

    /// <summary>
    /// Quad (four-channel) layout: front left, front right, back left, and back right.
    /// </summary>
    Quad = FrontLeft | FrontRight | BackLeft | BackRight,

    /// <summary>
    /// 5.0 surround layout: front left, front right, front center, back left, and back right.
    /// </summary>
    Surround50 = FrontLeft | FrontRight | FrontCenter | BackLeft | BackRight,

    /// <summary>
    /// 5.1 surround layout: front left, front right, front center, low frequency, back left, and back right.
    /// </summary>
    Surround51 = FrontLeft | FrontRight | FrontCenter | LowFrequency | BackLeft | BackRight,

    /// <summary>
    /// 7.1 surround layout: 5.1 layout plus side left and side right.
    /// </summary>
    Surround71 = Surround51 | SideLeft | SideRight,
}
