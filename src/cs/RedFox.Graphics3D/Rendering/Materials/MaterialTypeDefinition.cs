using System;
using RedFox.Graphics3D.Rendering;

namespace RedFox.Graphics3D.Rendering.Materials;

/// <summary>
/// Describes a named material type and the pipeline factory used to build it.
/// </summary>
public sealed record class MaterialTypeDefinition
{
    /// <summary>
    /// Gets the material type name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the backend-neutral material type descriptor when one is available.
    /// </summary>
    public MaterialTypeDescriptor? Descriptor { get; }

    /// <summary>
    /// Gets the backend pipeline factory for the material type.
    /// </summary>
    public Func<IGraphicsDevice, IGpuPipelineState> BuildPipeline { get; }

    /// <summary>
    /// Initializes a new <see cref="MaterialTypeDefinition"/> value.
    /// </summary>
    /// <param name="name">The material type name.</param>
    /// <param name="buildPipeline">The backend pipeline factory.</param>
    public MaterialTypeDefinition(string name, Func<IGraphicsDevice, IGpuPipelineState> buildPipeline)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(buildPipeline);

        Name = name;
        BuildPipeline = buildPipeline;
    }

    /// <summary>
    /// Initializes a new <see cref="MaterialTypeDefinition"/> value from a backend-neutral descriptor.
    /// </summary>
    /// <param name="descriptor">The material type descriptor.</param>
    /// <param name="shaderFactory">The backend shader factory.</param>
    public MaterialTypeDefinition(MaterialTypeDescriptor descriptor, IMaterialShaderFactory shaderFactory)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(shaderFactory);

        Name = descriptor.Name;
        Descriptor = descriptor;
        BuildPipeline = graphicsDevice => descriptor.BuildPipeline(graphicsDevice, shaderFactory);
    }
}