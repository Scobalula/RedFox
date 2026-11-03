using System.Numerics;
using System.Runtime.InteropServices;

namespace RedFox.Imaging.Vulkan;

/// <summary>
/// Uniform data consumed by image shaders.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct VulkanImageShaderConstants
{
    /// <summary>
    /// Gets or sets the image width in pixels.
    /// </summary>
    public uint Width;

    /// <summary>
    /// Gets or sets the image height in pixels.
    /// </summary>
    public uint Height;

    /// <summary>
    /// Gets or sets the shader specific flags.
    /// </summary>
    public uint Flags;

    /// <summary>
    /// Gets or sets the padding that aligns <see cref="Parameters"/> to 16 bytes.
    /// </summary>
    public uint Padding;

    /// <summary>
    /// Gets or sets the shader specific parameters.
    /// </summary>
    public Vector4 Parameters;
}
