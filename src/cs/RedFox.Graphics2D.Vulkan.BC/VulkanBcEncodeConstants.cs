using System.Runtime.InteropServices;

namespace RedFox.Graphics2D.BlockCompression.Vulkan;

/// <summary>
/// Uniform data consumed by the BC6H and BC7 encode compute shaders.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct VulkanBcEncodeConstants
{
    /// <summary>
    /// Gets or sets the source image width in pixels.
    /// </summary>
    public uint TextureWidth;

    /// <summary>
    /// Gets or sets the number of 4x4 blocks in the X direction.
    /// </summary>
    public uint BlockCountX;

    /// <summary>
    /// Gets or sets the BC format identifier expected by the shader.
    /// </summary>
    public uint Format;

    /// <summary>
    /// Gets or sets the mode identifier used by the current pass.
    /// </summary>
    public uint ModeId;

    /// <summary>
    /// Gets or sets the first block index processed by the dispatch.
    /// </summary>
    public uint StartBlockId;

    /// <summary>
    /// Gets or sets the total number of blocks in the texture.
    /// </summary>
    public uint TotalBlockCount;

    /// <summary>
    /// Gets or sets the BC7 alpha weighting factor.
    /// BC6H shaders ignore this field.
    /// </summary>
    public float AlphaWeight;

    /// <summary>
    /// Gets or sets the trailing padding required to match constant-buffer layout.
    /// </summary>
    public uint Padding;
}
