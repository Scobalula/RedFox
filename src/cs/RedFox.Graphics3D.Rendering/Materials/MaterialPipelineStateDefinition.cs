using RedFox.Graphics3D.Rendering;

namespace RedFox.Graphics3D.Rendering.Materials;

/// <summary>
/// Describes backend-neutral graphics pipeline state for a material type.
/// </summary>
public sealed record class MaterialPipelineStateDefinition
{
    /// <summary>
    /// Gets the triangle cull mode.
    /// </summary>
    public CullMode CullMode { get; }

    /// <summary>
    /// Gets the front-face winding order.
    /// </summary>
    public FaceWinding FaceWinding { get; }

    /// <summary>
    /// Gets a value indicating whether wireframe rasterization is enabled.
    /// </summary>
    public bool Wireframe { get; }

    /// <summary>
    /// Gets a value indicating whether color blending is enabled.
    /// </summary>
    public bool Blend { get; }

    /// <summary>
    /// Gets the source blend factor.
    /// </summary>
    public BlendFactor SourceBlendFactor { get; }

    /// <summary>
    /// Gets the destination blend factor.
    /// </summary>
    public BlendFactor DestinationBlendFactor { get; }

    /// <summary>
    /// Gets the color blend operation.
    /// </summary>
    public BlendOp BlendOperation { get; }

    /// <summary>
    /// Gets a value indicating whether depth testing is enabled.
    /// </summary>
    public bool DepthTest { get; }

    /// <summary>
    /// Gets a value indicating whether depth writes are enabled.
    /// </summary>
    public bool DepthWrite { get; }

    /// <summary>
    /// Gets the depth comparison function.
    /// </summary>
    public CompareFunc DepthCompareFunc { get; }

    /// <summary>
    /// Gets the primitive topology.
    /// </summary>
    public PrimitiveTopology PrimitiveTopology { get; }

    /// <summary>
    /// Initializes a new <see cref="MaterialPipelineStateDefinition"/> value.
    /// </summary>
    /// <param name="cullMode">The triangle cull mode.</param>
    /// <param name="faceWinding">The front-face winding order.</param>
    /// <param name="wireframe">Whether wireframe rasterization is enabled.</param>
    /// <param name="blend">Whether color blending is enabled.</param>
    /// <param name="sourceBlendFactor">The source blend factor.</param>
    /// <param name="destinationBlendFactor">The destination blend factor.</param>
    /// <param name="blendOperation">The color blend operation.</param>
    /// <param name="depthTest">Whether depth testing is enabled.</param>
    /// <param name="depthWrite">Whether depth writes are enabled.</param>
    /// <param name="depthCompareFunc">The depth comparison function.</param>
    /// <param name="primitiveTopology">The primitive topology.</param>
    public MaterialPipelineStateDefinition(
        CullMode cullMode,
        FaceWinding faceWinding,
        bool wireframe,
        bool blend,
        BlendFactor sourceBlendFactor,
        BlendFactor destinationBlendFactor,
        BlendOp blendOperation,
        bool depthTest,
        bool depthWrite,
        CompareFunc depthCompareFunc,
        PrimitiveTopology primitiveTopology)
    {
        CullMode = cullMode;
        FaceWinding = faceWinding;
        Wireframe = wireframe;
        Blend = blend;
        SourceBlendFactor = sourceBlendFactor;
        DestinationBlendFactor = destinationBlendFactor;
        BlendOperation = blendOperation;
        DepthTest = depthTest;
        DepthWrite = depthWrite;
        DepthCompareFunc = depthCompareFunc;
        PrimitiveTopology = primitiveTopology;
    }
}