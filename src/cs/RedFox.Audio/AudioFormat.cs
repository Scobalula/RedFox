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
/// Describes the format of audio data including sample rate, number of channels, and channel layout.
/// </summary>
/// <param name="SampleRate">The number of samples per second.</param>
/// <param name="Channels">The number of audio channels.</param>
/// <param name="Layout">The layout arrangement of the channels.</param>
public readonly record struct AudioFormat(int SampleRate, int Channels, ChannelLayout Layout)
{
    /// <summary>
    /// Initializes a new instance of the AudioFormat structure with the specified sample rate and number of channels,
    /// automatically determining an appropriate channel layout.
    /// </summary>
    /// <param name="sampleRate">The number of samples per second.</param>
    /// <param name="channels">The number of audio channels.</param>
    public AudioFormat(int sampleRate, int channels) : this(sampleRate, channels, GetDefaultLayout(channels))
    {
    }

    /// <summary>
    /// Gets the default channel layout for the specified number of channels.
    /// </summary>
    /// <param name="channels">The number of audio channels.</param>
    /// <returns>The appropriate channel layout for the number of channels, or <see cref="ChannelLayout.None"/> if not recognized.</returns>
    public static ChannelLayout GetDefaultLayout(int channels) => channels switch
    {
        1 => ChannelLayout.Mono,
        2 => ChannelLayout.Stereo,
        3 => ChannelLayout.Surround,
        4 => ChannelLayout.Quad,
        5 => ChannelLayout.Surround50,
        6 => ChannelLayout.Surround51,
        8 => ChannelLayout.Surround71,
        _ => ChannelLayout.None,
    };
}
