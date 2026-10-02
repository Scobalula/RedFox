namespace RedFox.Graphics3D.Rendering;

/// <summary>
/// Schedules buffer uploads across render frames while keeping backend resource calls on the render thread.
/// </summary>
public interface IFrameBufferUploadBudget
{
    /// <summary>
    /// Gets the remaining byte budget for the current frame.
    /// </summary>
    int RemainingBufferUploadBytes { get; }

    /// <summary>
    /// Starts a new frame's upload budget.
    /// </summary>
    void BeginFrameBufferUploads();

    /// <summary>
    /// Uploads as many complete buffer elements as fit in the current frame's remaining budget.
    /// </summary>
    /// <param name="buffer">The destination buffer.</param>
    /// <param name="byteOffset">The destination byte offset for the first source byte.</param>
    /// <param name="data">The source bytes beginning at <paramref name="byteOffset"/>.</param>
    /// <returns>The number of bytes uploaded.</returns>
    int UploadBufferRange(IGpuBuffer buffer, int byteOffset, ReadOnlySpan<byte> data);
}
