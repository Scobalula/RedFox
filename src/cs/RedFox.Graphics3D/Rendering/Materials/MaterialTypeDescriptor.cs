using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using RedFox.Graphics3D.Rendering.Backend;

namespace RedFox.Graphics3D.Rendering.Materials;

/// <summary>
/// Describes the shader stages, vertex inputs, and pipeline state for a material type.
/// </summary>
public sealed record class MaterialTypeDescriptor
{
    private readonly VertexAttribute[] _vertexAttributes;

    /// <summary>
    /// Initializes a new <see cref="MaterialTypeDescriptor"/> value.
    /// </summary>
    /// <param name="name">The material type name.</param>
    /// <param name="pipelineKind">The pipeline kind.</param>
    /// <param name="vertexShaderName">The vertex shader source name for graphics pipelines.</param>
    /// <param name="fragmentShaderName">The fragment shader source name for graphics pipelines.</param>
    /// <param name="computeShaderName">The compute shader source name for compute pipelines.</param>
    /// <param name="vertexAttributes">The vertex attributes consumed by graphics pipelines.</param>
    /// <param name="pipelineState">The graphics pipeline state.</param>
    /// <param name="requirements">The material input requirements.</param>
    [JsonConstructor]
    public MaterialTypeDescriptor(
        string name,
        MaterialPipelineKind pipelineKind,
        string? vertexShaderName,
        string? fragmentShaderName,
        string? computeShaderName,
        IReadOnlyList<VertexAttribute>? vertexAttributes,
        MaterialPipelineStateDefinition? pipelineState,
        MaterialTypeRequirements? requirements)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        requirements ??= MaterialTypeRequirements.Empty;
        Name = name;
        PipelineKind = pipelineKind;
        VertexShaderName = vertexShaderName;
        FragmentShaderName = fragmentShaderName;
        ComputeShaderName = computeShaderName;
        _vertexAttributes = CopyAttributes(vertexAttributes ?? Array.Empty<VertexAttribute>());
        PipelineState = pipelineState;
        Requirements = requirements;

        ValidatePipelineShape();
    }

    /// <summary>
    /// Gets the material type name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the kind of pipeline described by this material type.
    /// </summary>
    public MaterialPipelineKind PipelineKind { get; }

    /// <summary>
    /// Gets the vertex shader source name for graphics pipelines.
    /// </summary>
    public string? VertexShaderName { get; }

    /// <summary>
    /// Gets the fragment shader source name for graphics pipelines.
    /// </summary>
    public string? FragmentShaderName { get; }

    /// <summary>
    /// Gets the compute shader source name for compute pipelines.
    /// </summary>
    public string? ComputeShaderName { get; }

    /// <summary>
    /// Gets the vertex attributes consumed by graphics pipelines.
    /// </summary>
    public IReadOnlyList<VertexAttribute> VertexAttributes => _vertexAttributes;

    /// <summary>
    /// Gets the graphics pipeline state definition.
    /// </summary>
    public MaterialPipelineStateDefinition? PipelineState { get; }

    /// <summary>
    /// Gets the material input requirements declared by this material type.
    /// </summary>
    public MaterialTypeRequirements Requirements { get; }

    /// <summary>
    /// Creates a graphics material type descriptor.
    /// </summary>
    /// <param name="name">The material type name.</param>
    /// <param name="vertexShaderName">The vertex shader source name.</param>
    /// <param name="fragmentShaderName">The fragment shader source name.</param>
    /// <param name="vertexAttributes">The vertex input attributes.</param>
    /// <param name="pipelineState">The graphics pipeline state.</param>
    /// <returns>The created descriptor.</returns>
    public static MaterialTypeDescriptor CreateGraphics(
        string name,
        string vertexShaderName,
        string fragmentShaderName,
        IReadOnlyList<VertexAttribute> vertexAttributes,
        MaterialPipelineStateDefinition pipelineState)
    {
        return CreateGraphics(name, vertexShaderName, fragmentShaderName, vertexAttributes, pipelineState, MaterialTypeRequirements.Empty);
    }

    /// <summary>
    /// Creates a graphics material type descriptor.
    /// </summary>
    /// <param name="name">The material type name.</param>
    /// <param name="vertexShaderName">The vertex shader source name.</param>
    /// <param name="fragmentShaderName">The fragment shader source name.</param>
    /// <param name="vertexAttributes">The vertex input attributes.</param>
    /// <param name="pipelineState">The graphics pipeline state.</param>
    /// <param name="requirements">The material input requirements.</param>
    /// <returns>The created descriptor.</returns>
    public static MaterialTypeDescriptor CreateGraphics(
        string name,
        string vertexShaderName,
        string fragmentShaderName,
        IReadOnlyList<VertexAttribute> vertexAttributes,
        MaterialPipelineStateDefinition pipelineState,
        MaterialTypeRequirements requirements)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vertexShaderName);
        ArgumentException.ThrowIfNullOrWhiteSpace(fragmentShaderName);
        ArgumentNullException.ThrowIfNull(vertexAttributes);
        ArgumentNullException.ThrowIfNull(pipelineState);
        ArgumentNullException.ThrowIfNull(requirements);

        return new MaterialTypeDescriptor(
            name,
            MaterialPipelineKind.Graphics,
            vertexShaderName,
            fragmentShaderName,
            null,
            vertexAttributes,
            pipelineState,
            requirements);
    }

    /// <summary>
    /// Creates a compute material type descriptor.
    /// </summary>
    /// <param name="name">The material type name.</param>
    /// <param name="computeShaderName">The compute shader source name.</param>
    /// <returns>The created descriptor.</returns>
    public static MaterialTypeDescriptor CreateCompute(string name, string computeShaderName)
    {
        return CreateCompute(name, computeShaderName, MaterialTypeRequirements.Empty);
    }

    /// <summary>
    /// Creates a compute material type descriptor.
    /// </summary>
    /// <param name="name">The material type name.</param>
    /// <param name="computeShaderName">The compute shader source name.</param>
    /// <param name="requirements">The material input requirements.</param>
    /// <returns>The created descriptor.</returns>
    public static MaterialTypeDescriptor CreateCompute(string name, string computeShaderName, MaterialTypeRequirements requirements)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(computeShaderName);
        ArgumentNullException.ThrowIfNull(requirements);

        return new MaterialTypeDescriptor(
            name,
            MaterialPipelineKind.Compute,
            null,
            null,
            computeShaderName,
            Array.Empty<VertexAttribute>(),
                null,
                requirements);
    }

    internal IGpuPipelineState BuildPipeline(IGraphicsDevice graphicsDevice, IMaterialShaderFactory shaderFactory)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(shaderFactory);

        return PipelineKind switch
        {
            MaterialPipelineKind.Graphics => BuildGraphicsPipeline(graphicsDevice, shaderFactory),
            MaterialPipelineKind.Compute => BuildComputePipeline(graphicsDevice, shaderFactory),
            _ => throw new InvalidOperationException($"Unsupported material pipeline kind '{PipelineKind}'."),
        };
    }

    private static VertexAttribute[] CopyAttributes(IReadOnlyList<VertexAttribute> vertexAttributes)
    {
        ArgumentNullException.ThrowIfNull(vertexAttributes);

        VertexAttribute[] copy = new VertexAttribute[vertexAttributes.Count];
        for (int index = 0; index < vertexAttributes.Count; index++)
        {
            copy[index] = vertexAttributes[index];
        }

        return copy;
    }

    private void ValidatePipelineShape()
    {
        if (PipelineKind == MaterialPipelineKind.Graphics)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(VertexShaderName);
            ArgumentException.ThrowIfNullOrWhiteSpace(FragmentShaderName);
            if (PipelineState is null)
            {
                throw new ArgumentException($"Graphics material type '{Name}' must define pipeline state.");
            }

            if (_vertexAttributes.Length == 0)
            {
                throw new ArgumentException($"Graphics material type '{Name}' must define at least one vertex attribute.");
            }
        }
        else if (PipelineKind == MaterialPipelineKind.Compute)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(ComputeShaderName);
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(PipelineKind), PipelineKind, "Unsupported material pipeline kind.");
        }
    }

    private IGpuPipelineState BuildGraphicsPipeline(IGraphicsDevice graphicsDevice, IMaterialShaderFactory shaderFactory)
    {
        string vertexShaderName = VertexShaderName ?? throw new InvalidOperationException($"Material type '{Name}' does not define a vertex shader.");
        string fragmentShaderName = FragmentShaderName ?? throw new InvalidOperationException($"Material type '{Name}' does not define a fragment shader.");
        MaterialPipelineStateDefinition pipelineState = PipelineState ?? throw new InvalidOperationException($"Material type '{Name}' does not define graphics pipeline state.");

        IGpuShader vertexShader = shaderFactory.CreateShader(graphicsDevice, vertexShaderName, ShaderStage.Vertex);
        IGpuShader fragmentShader = shaderFactory.CreateShader(graphicsDevice, fragmentShaderName, ShaderStage.Fragment);
        try
        {
            return graphicsDevice.CreatePipelineState(
                vertexShader,
                fragmentShader,
                _vertexAttributes,
                pipelineState.CullMode,
                pipelineState.FaceWinding,
                pipelineState.Wireframe,
                pipelineState.Blend,
                pipelineState.SourceBlendFactor,
                pipelineState.DestinationBlendFactor,
                pipelineState.BlendOperation,
                pipelineState.DepthTest,
                pipelineState.DepthWrite,
                pipelineState.DepthCompareFunc,
                pipelineState.PrimitiveTopology);
        }
        finally
        {
            vertexShader.Dispose();
            fragmentShader.Dispose();
        }
    }

    private IGpuPipelineState BuildComputePipeline(IGraphicsDevice graphicsDevice, IMaterialShaderFactory shaderFactory)
    {
        string computeShaderName = ComputeShaderName ?? throw new InvalidOperationException($"Material type '{Name}' does not define a compute shader.");

        IGpuShader computeShader = shaderFactory.CreateShader(graphicsDevice, computeShaderName, ShaderStage.Compute);
        try
        {
            return graphicsDevice.CreatePipelineState(computeShader);
        }
        finally
        {
            computeShader.Dispose();
        }
    }
}