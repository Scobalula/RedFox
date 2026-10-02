using RedFox.Imaging;

namespace RedFox.Graphics3D.Rendering;

/// <summary>
/// Adds staged image allocation and uploads to a frame-budgeted graphics device.
/// </summary>
public interface IFrameTextureUploadBudget : IFrameBufferUploadBudget
{
    /// <summary>
    /// Allocates texture storage without uploading the image payload.
    /// </summary>
    /// <param name="image">The image layout to allocate.</param>
    /// <param name="usage">The intended texture usage.</param>
    /// <returns>The allocated GPU texture.</returns>
    IGpuTexture CreateTextureStorage(Image image, TextureUsage usage);

    /// <summary>
    /// Uploads the next row-aligned portion of an image slice that fits in the current frame budget.
    /// </summary>
    /// <param name="texture">The destination texture.</param>
    /// <param name="image">The source image.</param>
    /// <param name="sliceIndex">The source subresource index.</param>
    /// <param name="byteOffset">The byte offset within the source slice.</param>
    /// <returns>The number of source bytes uploaded.</returns>
    int UploadTextureRange(IGpuTexture texture, Image image, int sliceIndex, int byteOffset);

}
