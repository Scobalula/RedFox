using RedFox.Imaging;
using RedFox.Imaging.Primitives;

namespace RedFox.Graphics3D.Rendering;

/// <summary>
/// Adds progressive mip exposure and standalone slice uploads to a frame-budgeted graphics device.
/// </summary>
public interface IProgressiveTextureUploadBudget : IFrameTextureUploadBudget
{
    /// <summary>
    /// Allocates storage for one 2D image slice without uploading its pixels.
    /// </summary>
    /// <param name="width">The slice width.</param>
    /// <param name="height">The slice height.</param>
    /// <param name="format">The slice format.</param>
    /// <param name="usage">The intended texture usage.</param>
    /// <returns>The allocated GPU texture.</returns>
    IGpuTexture CreateTextureStorage(int width, int height, ImageFormat format, TextureUsage usage);

    /// <summary>
    /// Restricts sampling to the mip levels that have completed upload.
    /// </summary>
    /// <param name="texture">The destination texture.</param>
    /// <param name="baseMipLevel">The first available mip level.</param>
    /// <param name="maxMipLevel">The last available mip level.</param>
    void SetTextureMipRange(IGpuTexture texture, int baseMipLevel, int maxMipLevel);

    /// <summary>
    /// Uploads the next row-aligned portion of a standalone 2D slice that fits in the current frame budget.
    /// </summary>
    /// <param name="texture">The destination texture.</param>
    /// <param name="slice">The source slice.</param>
    /// <param name="byteOffset">The byte offset within the source slice.</param>
    /// <returns>The number of source bytes uploaded.</returns>
    int UploadTextureRange(IGpuTexture texture, ImageSlice slice, int byteOffset);
}
