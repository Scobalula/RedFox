using RedFox.Graphics3D;
using RedFox.Graphics3D.OpenGL.Resources;
using RedFox.Graphics3D.Rendering;
using System;

namespace RedFox.Graphics3D.OpenGL;

/// <summary>
/// Represents a concrete OpenGL graphics or compute pipeline state.
/// </summary>
internal sealed class OpenGlPipelineState : IGpuPipelineState
{
    private readonly VertexAttribute[] _vertexAttributes;

    /// <summary>
    /// Gets the compiled graphics program when this is a graphics pipeline.
    /// </summary>
    internal GlShaderProgram? GraphicsProgram { get; }

    /// <summary>
    /// Gets the compiled compute program when this is a compute pipeline.
    /// </summary>
    internal GlComputeProgram? ComputeProgram { get; }

    /// <summary>
    /// Gets the declared vertex attribute layout.
    /// </summary>
    internal ReadOnlySpan<VertexAttribute> VertexAttributes => _vertexAttributes;

    /// <summary>
    /// Gets the pipeline cull mode.
    /// </summary>
    internal CullMode CullMode { get; }

    /// <summary>
    /// Gets the pipeline front-face winding.
    /// </summary>
    internal FaceWinding FaceWinding { get; }

    /// <summary>
    /// Gets a value indicating whether wireframe rasterization is enabled.
    /// </summary>
    internal bool Wireframe { get; }

    /// <summary>
    /// Gets a value indicating whether color blending is enabled.
    /// </summary>
    internal bool BlendEnabled { get; }

    /// <summary>
    /// Gets the source blend factor.
    /// </summary>
    internal BlendFactor SourceBlendFactor { get; }

    /// <summary>
    /// Gets the destination blend factor.
    /// </summary>
    internal BlendFactor DestinationBlendFactor { get; }

    /// <summary>
    /// Gets the color blend operation.
    /// </summary>
    internal BlendOp BlendOperation { get; }

    /// <summary>
    /// Gets a value indicating whether depth testing is enabled.
    /// </summary>
    internal bool DepthTest { get; }

    /// <summary>
    /// Gets a value indicating whether depth writes are enabled.
    /// </summary>
    internal bool DepthWrite { get; }

    /// <summary>
    /// Gets the depth comparison function.
    /// </summary>
    internal CompareFunc DepthCompareFunc { get; }

    /// <summary>
    /// Gets the primitive topology.
    /// </summary>
    internal PrimitiveTopology PrimitiveTopology { get; }

    /// <summary>
    /// Gets a value indicating whether this is a compute pipeline.
    /// </summary>
    internal bool IsCompute => ComputeProgram is not null;

    /// <inheritdoc/>
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenGlPipelineState"/> class.
    /// </summary>
    /// <param name="graphicsProgram">The compiled graphics program.</param>
    /// <param name="vertexAttributes">The vertex attribute layout.</param>
    /// <param name="cullMode">The cull mode.</param>
    /// <param name="faceWinding">The front-face winding.</param>
    /// <param name="wireframe">Whether wireframe rasterization is enabled.</param>
    /// <param name="blendEnabled">Whether blending is enabled.</param>
    /// <param name="sourceBlendFactor">The source blend factor.</param>
    /// <param name="destinationBlendFactor">The destination blend factor.</param>
    /// <param name="blendOperation">The blend operation.</param>
    /// <param name="depthTest">Whether depth testing is enabled.</param>
    /// <param name="depthWrite">Whether depth writes are enabled.</param>
    /// <param name="depthCompareFunc">The depth comparison function.</param>
    /// <param name="primitiveTopology">The primitive topology.</param>
    public OpenGlPipelineState(
        GlShaderProgram graphicsProgram,
        ReadOnlySpan<VertexAttribute> vertexAttributes,
        CullMode cullMode,
        FaceWinding faceWinding,
        bool wireframe,
        bool blendEnabled,
        BlendFactor sourceBlendFactor,
        BlendFactor destinationBlendFactor,
        BlendOp blendOperation,
        bool depthTest,
        bool depthWrite,
        CompareFunc depthCompareFunc,
        PrimitiveTopology primitiveTopology)
    {
        GraphicsProgram = graphicsProgram ?? throw new ArgumentNullException(nameof(graphicsProgram));
        _vertexAttributes = vertexAttributes.ToArray();
        CullMode = cullMode;
        FaceWinding = faceWinding;
        Wireframe = wireframe;
        BlendEnabled = blendEnabled;
        SourceBlendFactor = sourceBlendFactor;
        DestinationBlendFactor = destinationBlendFactor;
        BlendOperation = blendOperation;
        DepthTest = depthTest;
        DepthWrite = depthWrite;
        DepthCompareFunc = depthCompareFunc;
        PrimitiveTopology = primitiveTopology;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenGlPipelineState"/> class.
    /// </summary>
    /// <param name="computeProgram">The compiled compute program.</param>
    public OpenGlPipelineState(GlComputeProgram computeProgram)
    {
        ComputeProgram = computeProgram ?? throw new ArgumentNullException(nameof(computeProgram));
        _vertexAttributes = [];
        CullMode = CullMode.None;
        FaceWinding = FaceWinding.CounterClockwise;
        BlendOperation = BlendOp.Add;
        SourceBlendFactor = BlendFactor.One;
        DestinationBlendFactor = BlendFactor.Zero;
        PrimitiveTopology = PrimitiveTopology.Triangles;
        DepthCompareFunc = CompareFunc.LessOrEqual;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        GraphicsProgram?.Dispose();
        ComputeProgram?.Dispose();
        IsDisposed = true;
        GC.SuppressFinalize(this);
    }
}