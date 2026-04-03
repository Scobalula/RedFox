namespace RedFox.Graphics2D.BlockCompression.Vulkan;

/// <summary>
/// Options for <see cref="VulkanBcConverterEngine"/>.
/// </summary>
public sealed class VulkanBcConverterEngineOptions
{
    /// <summary>
    /// When true, the engine falls back to the built-in CPU codecs when a BC conversion
    /// is not handled by the Vulkan path or when Vulkan initialization is unavailable.
    /// </summary>
    public bool AllowCpuBridgeConversion { get; init; } = true;
}
