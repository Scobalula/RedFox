using System.Numerics;

namespace RedFox.Graphics3D.Rendering;

/// <summary>
/// Describes how an <see cref="ImageRenderer"/> displays an image slice.
/// </summary>
/// <param name="ChannelMask">The red, green, blue, and alpha channels to display, each either zero or one.</param>
/// <param name="Exposure">The exposure adjustment in stops applied to the color channels.</param>
/// <param name="ReconstructNormal">Whether the blue channel is rebuilt from the red and green channels of a two-channel normal map.</param>
/// <param name="Tile">Whether the image is repeated in a three by three grid.</param>
/// <param name="FlipY">Whether the image is displayed upside down.</param>
public readonly record struct ImageViewOptions(Vector4 ChannelMask, float Exposure, bool ReconstructNormal, bool Tile, bool FlipY)
{
    /// <summary>
    /// Gets the options that display every channel unmodified.
    /// </summary>
    public static ImageViewOptions Default { get; } = new(Vector4.One, 0.0f, false, false, false);
}
