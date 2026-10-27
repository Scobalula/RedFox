using System.Numerics;
using RedFox.Imaging.Processing;

namespace RedFox.Graphics3D;

/// <summary>
/// Describes which texture channels feed a material input and how their values are remapped.
/// Each component of the input reads one channel, then becomes <c>channel * Scale + Bias</c>.
/// </summary>
public sealed record class MaterialChannelSource
{
    /// <summary>
    /// Gets the slot key of the material texture the input reads.
    /// </summary>
    public string TextureName { get; }

    /// <summary>
    /// Gets the channels read into the input's components, in order. The last channel repeats when fewer than four are given.
    /// </summary>
    public IReadOnlyList<ColorChannel> Channels { get; }

    /// <summary>
    /// Gets the per-component scale applied to the read channels.
    /// </summary>
    public Vector4 Scale { get; init; } = Vector4.One;

    /// <summary>
    /// Gets the per-component bias added after scaling.
    /// </summary>
    public Vector4 Bias { get; init; }

    /// <summary>
    /// Initializes a new <see cref="MaterialChannelSource"/> value.
    /// </summary>
    /// <param name="textureName">The slot key of the material texture the input reads.</param>
    /// <param name="channels">The one to four channels read into the input's components, in order.</param>
    public MaterialChannelSource(string textureName, params ColorChannel[] channels)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(textureName);
        ArgumentNullException.ThrowIfNull(channels);

        if (channels.Length is < 1 or > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(channels), channels.Length, "A channel source reads between one and four channels.");
        }

        TextureName	= textureName;
        Channels	= [.. channels];
    }
}
