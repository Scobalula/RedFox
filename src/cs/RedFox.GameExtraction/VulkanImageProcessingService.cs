using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using RedFox.Imaging;
using RedFox.Imaging.Vulkan;

namespace RedFox.GameExtraction;

/// <summary>
/// Owns the Vulkan context used to run image shaders, and the shaders created on it.
/// </summary>
public sealed class VulkanImageProcessingService : IDisposable
{
    private readonly VulkanBcContext? _context;
    private readonly Dictionary<string, VulkanImageShader> _shaders = [];
    private readonly Lock _lock = new();

    /// <summary>
    /// Gets whether a Vulkan device that can sample BC images is available.
    /// </summary>
    public bool IsAvailable { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="VulkanImageProcessingService"/> class, creating its Vulkan context.
    /// </summary>
    public VulkanImageProcessingService()
    {
        _context	= VulkanBcContext.Create();
        IsAvailable	= _context is { SupportsBcSampling: true };
    }

    /// <summary>
    /// Runs a shader over the image's first mip, creating the shader on its first use.
    /// </summary>
    /// <param name="shaderName">The name the shader is cached under.</param>
    /// <param name="spirv">The compiled SPIR-V of the shader.</param>
    /// <param name="outputCount">The number of images the shader writes.</param>
    /// <param name="input">The image to read, in its stored format.</param>
    /// <param name="parameters">The shader specific parameters.</param>
    /// <param name="flags">The shader specific flags.</param>
    /// <returns>The images the shader wrote.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no suitable Vulkan device is available.</exception>
    public Image[] Run(string shaderName, byte[] spirv, int outputCount, Image input, Vector4 parameters, uint flags)
    {
        if (_context is not { SupportsBcSampling: true })
        {
            throw new InvalidOperationException("No Vulkan device that can sample BC images is available.");
        }

        lock (_lock)
        {
            if (!_shaders.TryGetValue(shaderName, out VulkanImageShader? shader))
            {
                shader					= new VulkanImageShader(_context, spirv, outputCount);
                _shaders[shaderName]	= shader;
            }

            return shader.Run(input, parameters, flags);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (VulkanImageShader shader in _shaders.Values)
        {
            shader.Dispose();
        }

        _context?.Dispose();
    }
}
