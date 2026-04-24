using System;
using RedFox.Graphics3D.Rendering.Backend;

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
        Name = name;
        BuildPipeline = buildPipeline;
    }
}