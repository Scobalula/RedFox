using System.Collections.Generic;
using RedFox.Graphics3D.Rendering.Backend;
using RedFox.Graphics3D.Rendering.Materials;

namespace RedFox.Graphics3D.D3D11;

/// <summary>
/// Represents the Direct3D 11 material-type registry.
/// </summary>
public sealed class D3D11MaterialTypeRegistry : IMaterialTypeRegistry
{
    private static readonly VertexAttribute[] DefaultVertexAttributes =
    [
        new VertexAttribute("Positions", 3, VertexAttributeType.Float32, 0, 3 * sizeof(float)),
        new VertexAttribute("Normals", 3, VertexAttributeType.Float32, 0, 3 * sizeof(float)),
    ];

    private static readonly VertexAttribute[] LineVertexAttributes =
    [
        new VertexAttribute("LineStart", 3, VertexAttributeType.Float32, 0, 13 * sizeof(float)),
        new VertexAttribute("LineEnd", 3, VertexAttributeType.Float32, 3 * sizeof(float), 13 * sizeof(float)),
        new VertexAttribute("Color", 4, VertexAttributeType.Float32, 6 * sizeof(float), 13 * sizeof(float)),
        new VertexAttribute("Along", 1, VertexAttributeType.Float32, 10 * sizeof(float), 13 * sizeof(float)),
        new VertexAttribute("Side", 1, VertexAttributeType.Float32, 11 * sizeof(float), 13 * sizeof(float)),
        new VertexAttribute("WidthScale", 1, VertexAttributeType.Float32, 12 * sizeof(float), 13 * sizeof(float)),
    ];

    private readonly Dictionary<string, MaterialTypeDefinition> _definitions = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="D3D11MaterialTypeRegistry"/> class.
    /// </summary>
    public D3D11MaterialTypeRegistry()
    {
        Register(CreateDefaultDefinition());
        Register(CreateGridDefinition());
        Register(CreateSkeletonDefinition());
        Register(CreateSkinningDefinition());
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<string> RegisteredNames => _definitions.Keys;

    /// <inheritdoc/>
    public void Register(MaterialTypeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _definitions[definition.Name] = definition;
    }

    /// <inheritdoc/>
    public MaterialTypeDefinition Get(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _definitions[name];
    }

    /// <inheritdoc/>
    public bool Contains(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _definitions.ContainsKey(name);
    }

    private static MaterialTypeDefinition CreateDefaultDefinition()
    {
        return new MaterialTypeDefinition(
            "Default",
            static graphicsDevice =>
            {
                D3D11GraphicsDevice d3dGraphicsDevice = GetD3D11GraphicsDevice(graphicsDevice);
                IGpuShader vertexShader = d3dGraphicsDevice.CreateShaderFromFile(HlslShaderSourceLoader.GetVertexSourcePath("Default"), ShaderStage.Vertex);
                IGpuShader fragmentShader = d3dGraphicsDevice.CreateShaderFromFile(HlslShaderSourceLoader.GetFragmentSourcePath("Default"), ShaderStage.Fragment);
                try
                {
                    return graphicsDevice.CreatePipelineState(
                        vertexShader,
                        fragmentShader,
                        DefaultVertexAttributes,
                        CullMode.Back,
                        FaceWinding.CounterClockwise,
                        false,
                        false,
                        BlendFactor.One,
                        BlendFactor.Zero,
                        BlendOp.Add,
                        true,
                        true,
                        CompareFunc.LessOrEqual,
                        PrimitiveTopology.Triangles);
                }
                finally
                {
                    vertexShader.Dispose();
                    fragmentShader.Dispose();
                }
            });
    }

    private static MaterialTypeDefinition CreateGridDefinition()
    {
        return new MaterialTypeDefinition(
            "Grid",
            static graphicsDevice =>
            {
                D3D11GraphicsDevice d3dGraphicsDevice = GetD3D11GraphicsDevice(graphicsDevice);
                IGpuShader vertexShader = d3dGraphicsDevice.CreateShaderFromFile(HlslShaderSourceLoader.GetVertexSourcePath("Grid"), ShaderStage.Vertex);
                IGpuShader fragmentShader = d3dGraphicsDevice.CreateShaderFromFile(HlslShaderSourceLoader.GetFragmentSourcePath("Grid"), ShaderStage.Fragment);
                try
                {
                    return graphicsDevice.CreatePipelineState(
                        vertexShader,
                        fragmentShader,
                        LineVertexAttributes,
                        CullMode.None,
                        FaceWinding.CounterClockwise,
                        false,
                        true,
                        BlendFactor.SourceAlpha,
                        BlendFactor.InverseSourceAlpha,
                        BlendOp.Add,
                        true,
                        false,
                        CompareFunc.LessOrEqual,
                        PrimitiveTopology.Triangles);
                }
                finally
                {
                    vertexShader.Dispose();
                    fragmentShader.Dispose();
                }
            });
    }

    private static MaterialTypeDefinition CreateSkeletonDefinition()
    {
        return new MaterialTypeDefinition(
            "Skeleton",
            static graphicsDevice =>
            {
                D3D11GraphicsDevice d3dGraphicsDevice = GetD3D11GraphicsDevice(graphicsDevice);
                IGpuShader vertexShader = d3dGraphicsDevice.CreateShaderFromFile(HlslShaderSourceLoader.GetVertexSourcePath("Skeleton"), ShaderStage.Vertex);
                IGpuShader fragmentShader = d3dGraphicsDevice.CreateShaderFromFile(HlslShaderSourceLoader.GetFragmentSourcePath("Skeleton"), ShaderStage.Fragment);
                try
                {
                    return graphicsDevice.CreatePipelineState(
                        vertexShader,
                        fragmentShader,
                        LineVertexAttributes,
                        CullMode.None,
                        FaceWinding.CounterClockwise,
                        false,
                        true,
                        BlendFactor.SourceAlpha,
                        BlendFactor.InverseSourceAlpha,
                        BlendOp.Add,
                        false,
                        false,
                        CompareFunc.LessOrEqual,
                        PrimitiveTopology.Triangles);
                }
                finally
                {
                    vertexShader.Dispose();
                    fragmentShader.Dispose();
                }
            });
    }

    private static MaterialTypeDefinition CreateSkinningDefinition()
    {
        return new MaterialTypeDefinition(
            "Skinning",
            static graphicsDevice =>
            {
                D3D11GraphicsDevice d3dGraphicsDevice = GetD3D11GraphicsDevice(graphicsDevice);
                IGpuShader computeShader = d3dGraphicsDevice.CreateShaderFromFile(HlslShaderSourceLoader.GetComputeSourcePath("Skinning"), ShaderStage.Compute);
                try
                {
                    return graphicsDevice.CreatePipelineState(computeShader);
                }
                finally
                {
                    computeShader.Dispose();
                }
            });
    }

    private static D3D11GraphicsDevice GetD3D11GraphicsDevice(IGraphicsDevice graphicsDevice)
    {
        return graphicsDevice as D3D11GraphicsDevice
            ?? throw new InvalidOperationException($"Expected {nameof(D3D11GraphicsDevice)}.");
    }
}