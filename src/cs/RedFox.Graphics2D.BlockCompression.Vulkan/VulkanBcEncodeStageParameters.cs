namespace RedFox.Graphics2D.BlockCompression.Vulkan;

/// <summary>
/// Groups the texture dimensions and block counts that parameterize the BC encode pass pipeline.
/// </summary>
public readonly struct VulkanBcEncodeStageParameters
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VulkanBcEncodeStageParameters"/> struct.
    /// </summary>
    /// <param name="textureWidth">The source image width in pixels.</param>
    /// <param name="blockCountX">The number of 4x4 blocks in the X direction.</param>
    /// <param name="totalBlockCount">The total number of 4x4 blocks in the texture.</param>
    /// <param name="shaderFormatId">The BC format identifier expected by the shader.</param>
    public VulkanBcEncodeStageParameters(uint textureWidth, uint blockCountX, uint totalBlockCount, uint shaderFormatId)
    {
        TextureWidth = textureWidth;
        BlockCountX = blockCountX;
        TotalBlockCount = totalBlockCount;
        ShaderFormatId = shaderFormatId;
    }

    /// <summary>
    /// Gets the source image width in pixels.
    /// </summary>
    public uint TextureWidth { get; }

    /// <summary>
    /// Gets the number of 4x4 blocks in the X direction.
    /// </summary>
    public uint BlockCountX { get; }

    /// <summary>
    /// Gets the total number of 4x4 blocks in the texture.
    /// </summary>
    public uint TotalBlockCount { get; }

    /// <summary>
    /// Gets the BC format identifier expected by the shader.
    /// </summary>
    public uint ShaderFormatId { get; }
}
