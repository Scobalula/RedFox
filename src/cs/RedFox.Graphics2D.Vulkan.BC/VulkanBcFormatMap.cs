using Silk.NET.Vulkan;

namespace RedFox.Graphics2D.BlockCompression.Vulkan;

/// <summary>
/// Maps RedFox image formats onto the subset of Vulkan formats used by the BC converter engine.
/// </summary>
public static class VulkanBcFormatMap
{
    /// <summary>
    /// Returns <see langword="true"/> when the format belongs to the BC1-BC7 family.
    /// </summary>
    /// <param name="format">The format to inspect.</param>
    /// <returns><see langword="true"/> when the format is BC-compressed; otherwise <see langword="false"/>.</returns>
    public static bool IsBcFormat(ImageFormat format)
    {
        return format is >= ImageFormat.BC1Typeless and <= ImageFormat.BC5Snorm or >= ImageFormat.BC6HTypeless and <= ImageFormat.BC7UnormSrgb;
    }

    /// <summary>
    /// Returns <see langword="true"/> when the format can be produced directly by the GPU BC decode path.
    /// </summary>
    /// <param name="format">The destination format to inspect.</param>
    /// <returns><see langword="true"/> when the GPU decode path can write the format directly.</returns>
    public static bool IsGpuDecodeTarget(ImageFormat format)
    {
        return format is ImageFormat.R8G8B8A8Unorm or ImageFormat.R8G8B8A8UnormSrgb or ImageFormat.R16G16B16A16Float or ImageFormat.R32G32B32A32Float;
    }

    /// <summary>
    /// Returns <see langword="true"/> when the format pair is handled directly by the GPU decode path.
    /// </summary>
    /// <param name="sourceFormat">The source format.</param>
    /// <param name="destinationFormat">The destination format.</param>
    /// <returns><see langword="true"/> when the pair is supported by the GPU decode path.</returns>
    public static bool CanDecode(ImageFormat sourceFormat, ImageFormat destinationFormat)
    {
        return IsBcFormat(sourceFormat) && IsGpuDecodeTarget(destinationFormat);
    }

    /// <summary>
    /// Returns <see langword="true"/> when the format pair is handled directly by the GPU encode path.
    /// </summary>
    /// <param name="sourceFormat">The source format.</param>
    /// <param name="destinationFormat">The destination format.</param>
    /// <returns><see langword="true"/> when the pair is supported by the GPU encode path.</returns>
    public static bool CanEncode(ImageFormat sourceFormat, ImageFormat destinationFormat)
    {
        return destinationFormat switch
        {
            ImageFormat.BC6HTypeless or
            ImageFormat.BC6HUF16 => sourceFormat is ImageFormat.R16G16B16A16Float or ImageFormat.R32G32B32A32Float,
            ImageFormat.BC6HSF16 => sourceFormat is ImageFormat.R16G16B16A16Float or ImageFormat.R32G32B32A32Float,
            ImageFormat.BC7Typeless or
            ImageFormat.BC7Unorm or
            ImageFormat.BC7UnormSrgb => sourceFormat is ImageFormat.R8G8B8A8Unorm or ImageFormat.R8G8B8A8UnormSrgb,
            _ => false,
        };
    }

    /// <summary>
    /// Attempts to map a BC source format to the Vulkan sampled image format used by the decode path.
    /// sRGB and typeless variants are normalized to the matching linear sampled format.
    /// </summary>
    /// <param name="format">The RedFox source format.</param>
    /// <param name="vulkanFormat">The mapped Vulkan format when successful.</param>
    /// <returns><see langword="true"/> when the format can be sampled by the GPU decode path.</returns>
    public static bool TryGetDecodeSourceFormat(ImageFormat format, out Format vulkanFormat)
    {
        vulkanFormat = format switch
        {
            ImageFormat.BC1Typeless or
            ImageFormat.BC1Unorm or
            ImageFormat.BC1UnormSrgb => Format.BC1RgbaUnormBlock,

            ImageFormat.BC2Typeless or
            ImageFormat.BC2Unorm or
            ImageFormat.BC2UnormSrgb => Format.BC2UnormBlock,

            ImageFormat.BC3Typeless or
            ImageFormat.BC3Unorm or
            ImageFormat.BC3UnormSrgb => Format.BC3UnormBlock,

            ImageFormat.BC4Typeless or
            ImageFormat.BC4Unorm => Format.BC4UnormBlock,
            ImageFormat.BC4Snorm => Format.BC4SNormBlock,

            ImageFormat.BC5Typeless or
            ImageFormat.BC5Unorm => Format.BC5UnormBlock,
            ImageFormat.BC5Snorm => Format.BC5SNormBlock,

            ImageFormat.BC6HTypeless or
            ImageFormat.BC6HUF16 => Format.BC6HUfloatBlock,
            ImageFormat.BC6HSF16 => Format.BC6HSfloatBlock,

            ImageFormat.BC7Typeless or
            ImageFormat.BC7Unorm or
            ImageFormat.BC7UnormSrgb => Format.BC7UnormBlock,
            _ => Format.Undefined,
        };

        return vulkanFormat != Format.Undefined;
    }

    /// <summary>
    /// Attempts to map a GPU decode target format to the output Vulkan format, shader resource, and byte size.
    /// </summary>
    /// <param name="format">The destination format.</param>
    /// <param name="vulkanFormat">The mapped Vulkan format when the source must be uploaded as an image.</param>
    /// <param name="shaderResourceName">The embedded SPIR-V resource name used for the decode shader.</param>
    /// <param name="bytesPerPixel">The tightly packed output bytes per pixel written by the shader.</param>
    /// <returns><see langword="true"/> when the target format is supported by the GPU decode path.</returns>
    public static bool TryGetDecodeTarget(ImageFormat format, out Format vulkanFormat, out string shaderResourceName, out int bytesPerPixel)
    {
        switch (format)
        {
            case ImageFormat.R8G8B8A8Unorm:
            case ImageFormat.R8G8B8A8UnormSrgb:
                vulkanFormat = Format.R8G8B8A8Unorm;
                shaderResourceName = "BcDecodeRgba8.spv";
                bytesPerPixel = 4;
                return true;

            case ImageFormat.R16G16B16A16Float:
                vulkanFormat = Format.R16G16B16A16Sfloat;
                shaderResourceName = "BcDecodeRgba16.spv";
                bytesPerPixel = 8;
                return true;

            case ImageFormat.R32G32B32A32Float:
                vulkanFormat = Format.R32G32B32A32Sfloat;
                shaderResourceName = "BcDecodeRgba32.spv";
                bytesPerPixel = 16;
                return true;

            default:
                vulkanFormat = Format.Undefined;
                shaderResourceName = string.Empty;
                bytesPerPixel = 0;
                return false;
        }
    }

    /// <summary>
    /// Attempts to map a GPU encode source format to the Vulkan sampled image format used by the compute shaders.
    /// sRGB variants are normalized to their linear upload formats to avoid implicit colorspace conversion during sampling.
    /// </summary>
    /// <param name="format">The source format.</param>
    /// <param name="vulkanFormat">The mapped Vulkan format when successful.</param>
    /// <returns><see langword="true"/> when the format can be sampled by the GPU encode path.</returns>
    public static bool TryGetEncodeSourceFormat(ImageFormat format, out Format vulkanFormat)
    {
        vulkanFormat = format switch
        {
            ImageFormat.R8G8B8A8Unorm or
            ImageFormat.R8G8B8A8UnormSrgb => Format.R8G8B8A8Unorm,

            ImageFormat.R16G16B16A16Float => Format.R16G16B16A16Sfloat,
            ImageFormat.R32G32B32A32Float => Format.R32G32B32A32Sfloat,
            _ => Format.Undefined,
        };

        return vulkanFormat != Format.Undefined;
    }

    /// <summary>
    /// Attempts to map an encode target format to the shader family and format identifier expected by the HLSL kernels.
    /// </summary>
    /// <param name="format">The target BC format.</param>
    /// <param name="isBc7">Receives whether the BC7 shader family should be used.</param>
    /// <param name="shaderFormatId">Receives the shader-side BC format identifier.</param>
    /// <returns><see langword="true"/> when the target format is supported by the GPU encode path.</returns>
    public static bool TryGetEncodeTarget(ImageFormat format, out bool isBc7, out uint shaderFormatId)
    {
        switch (format)
        {
            case ImageFormat.BC6HTypeless:
            case ImageFormat.BC6HUF16:
                isBc7 = false;
                shaderFormatId = 95;
                return true;

            case ImageFormat.BC6HSF16:
                isBc7 = false;
                shaderFormatId = 96;
                return true;

            case ImageFormat.BC7Typeless:
            case ImageFormat.BC7Unorm:
            case ImageFormat.BC7UnormSrgb:
                isBc7 = true;
                shaderFormatId = 98;
                return true;

            default:
                isBc7 = false;
                shaderFormatId = 0;
                return false;
        }
    }
}
