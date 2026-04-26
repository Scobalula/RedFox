using System;
using System.Collections.Generic;
using System.IO;

namespace RedFox.Graphics3D.Rendering.Materials;

/// <summary>
/// Provides the built-in material types shared by all rendering backends.
/// </summary>
public static class BuiltInMaterialTypes
{
    private const string BuiltInDefinitionDirectory = "MaterialTypes";
    private static readonly Lazy<IReadOnlyList<MaterialTypeDescriptor>> Descriptors = new(LoadDescriptors);

    /// <summary>
    /// Creates material type definitions for the built-in material catalog.
    /// </summary>
    /// <param name="shaderFactory">The backend shader factory.</param>
    /// <returns>The material type definitions.</returns>
    public static IReadOnlyList<MaterialTypeDefinition> CreateDefinitions(IMaterialShaderFactory shaderFactory)
    {
        ArgumentNullException.ThrowIfNull(shaderFactory);
        return MaterialTypeJsonLoader.CreateDefinitions(Descriptors.Value, shaderFactory);
    }

    /// <summary>
    /// Registers the built-in material type definitions into an existing registry.
    /// </summary>
    /// <param name="registry">The registry to populate.</param>
    /// <param name="shaderFactory">The backend shader factory.</param>
    public static void Register(IMaterialTypeRegistry registry, IMaterialShaderFactory shaderFactory)
    {
        ArgumentNullException.ThrowIfNull(registry);

        IReadOnlyList<MaterialTypeDefinition> definitions = CreateDefinitions(shaderFactory);
        for (int index = 0; index < definitions.Count; index++)
        {
            registry.Register(definitions[index]);
        }
    }

    private static IReadOnlyList<MaterialTypeDescriptor> LoadDescriptors()
    {
        string path = Path.Combine(AppContext.BaseDirectory, BuiltInDefinitionDirectory);
        if (Directory.Exists(path))
        {
            return MaterialTypeJsonLoader.LoadDirectory(path);
        }

        string sourcePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Rendering", "Materials", "Definitions"));
        if (Directory.Exists(sourcePath))
        {
            return MaterialTypeJsonLoader.LoadDirectory(sourcePath);
        }

        throw new DirectoryNotFoundException($"Built-in material type definition directory was not found: {path}");
    }
}