using RedFox.Graphics3D;
using RedFox.Graphics3D.OpenGL.Shaders;
using RedFox.Graphics3D.Rendering.Backend;
using RedFox.Graphics3D.Rendering.Materials;
using System;
using System.Collections.Generic;
using System.Text;

namespace RedFox.Graphics3D.OpenGL;

/// <summary>
/// Represents the concrete OpenGL material-type registry.
/// </summary>
internal sealed class OpenGlMaterialTypeRegistry : IMaterialTypeRegistry
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
    /// Gets the registered material type names.
    /// </summary>
    public IReadOnlyCollection<string> RegisteredNames => _definitions.Keys;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenGlMaterialTypeRegistry"/> class.
    /// </summary>
    public OpenGlMaterialTypeRegistry()
    {
        Register(CreateDefaultDefinition());
        Register(CreateGridDefinition());
        Register(CreateSkeletonDefinition());
        Register(CreateSkinningDefinition());
    }

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
                IGpuShader vertexShader = graphicsDevice.CreateShader(Encode(GlslShaderSourceLoader.LoadVertexSource("Default")), ShaderStage.Vertex);
                IGpuShader fragmentShader = graphicsDevice.CreateShader(Encode(GlslShaderSourceLoader.LoadFragmentSource("Default")), ShaderStage.Fragment);
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
                IGpuShader vertexShader = graphicsDevice.CreateShader(Encode(GlslShaderSourceLoader.LoadVertexSource("Grid")), ShaderStage.Vertex);
                IGpuShader fragmentShader = graphicsDevice.CreateShader(Encode(GlslShaderSourceLoader.LoadFragmentSource("Grid")), ShaderStage.Fragment);
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
                IGpuShader vertexShader = graphicsDevice.CreateShader(Encode(GlslShaderSourceLoader.LoadVertexSource("Skeleton")), ShaderStage.Vertex);
                IGpuShader fragmentShader = graphicsDevice.CreateShader(Encode(GlslShaderSourceLoader.LoadFragmentSource("Skeleton")), ShaderStage.Fragment);
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

    private static MaterialTypeDefinition CreateSkinningDefinition()
    {
        return new MaterialTypeDefinition(
            "Skinning",
            static graphicsDevice =>
            {
                IGpuShader computeShader = graphicsDevice.CreateShader(Encode(GlslShaderSourceLoader.LoadComputeSource("Skinning")), ShaderStage.Compute);
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

    private static byte[] Encode(string source)
    {
        return Encoding.UTF8.GetBytes(source);
    }
}