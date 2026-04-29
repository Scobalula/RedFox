using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using RedFox.Graphics3D.Rendering;

namespace RedFox.Graphics3D.Rendering.Materials;

/// <summary>
/// Loads and saves single-material JSON material type definition files.
/// </summary>
public static class MaterialTypeJsonLoader
{
    private const string SearchPattern = "*.materialtype.json";
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    /// <summary>
    /// Loads a material type descriptor from a JSON file.
    /// </summary>
    /// <param name="path">The JSON material type definition file path.</param>
    /// <returns>The loaded material type descriptor.</returns>
    public static MaterialTypeDescriptor LoadFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string json = File.ReadAllText(path);
        return LoadString(json);
    }

    /// <summary>
    /// Loads a material type descriptor from JSON text.
    /// </summary>
    /// <param name="json">The JSON material type definition text.</param>
    /// <returns>The loaded material type descriptor.</returns>
    public static MaterialTypeDescriptor LoadString(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        return JsonSerializer.Deserialize<MaterialTypeDescriptor>(json, SerializerOptions)
            ?? throw new FormatException("Material type JSON did not contain a descriptor.");
    }

    /// <summary>
    /// Loads all material type descriptors from a directory of single-material JSON files.
    /// </summary>
    /// <param name="directoryPath">The directory containing material type JSON files.</param>
    /// <returns>The loaded material type descriptors.</returns>
    public static IReadOnlyList<MaterialTypeDescriptor> LoadDirectory(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException($"Material type definition directory was not found: {directoryPath}");
        }

        string[] files = Directory.GetFiles(directoryPath, SearchPattern, SearchOption.TopDirectoryOnly);
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);

        MaterialTypeDescriptor[] descriptors = new MaterialTypeDescriptor[files.Length];
        for (int index = 0; index < files.Length; index++)
        {
            descriptors[index] = LoadFile(files[index]);
        }

        return descriptors;
    }

    /// <summary>
    /// Serializes a material type descriptor into JSON text.
    /// </summary>
    /// <param name="descriptor">The descriptor to serialize.</param>
    /// <returns>The JSON material type definition text.</returns>
    public static string ToJson(MaterialTypeDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return JsonSerializer.Serialize(descriptor, SerializerOptions);
    }

    /// <summary>
    /// Saves a material type descriptor as a JSON file.
    /// </summary>
    /// <param name="descriptor">The descriptor to save.</param>
    /// <param name="path">The destination JSON file path.</param>
    public static void SaveFile(MaterialTypeDescriptor descriptor, string path)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        File.WriteAllText(path, ToJson(descriptor));
    }

    /// <summary>
    /// Creates material type definitions from descriptors for a backend shader factory.
    /// </summary>
    /// <param name="descriptors">The material type descriptors.</param>
    /// <param name="shaderFactory">The backend shader factory.</param>
    /// <returns>The backend-bound material type definitions.</returns>
    public static IReadOnlyList<MaterialTypeDefinition> CreateDefinitions(
        IReadOnlyList<MaterialTypeDescriptor> descriptors,
        IMaterialShaderFactory shaderFactory)
    {
        ArgumentNullException.ThrowIfNull(descriptors);
        ArgumentNullException.ThrowIfNull(shaderFactory);

        MaterialTypeDefinition[] definitions = new MaterialTypeDefinition[descriptors.Count];
        for (int index = 0; index < descriptors.Count; index++)
        {
            definitions[index] = new MaterialTypeDefinition(descriptors[index], shaderFactory);
        }

        return definitions;
    }

    /// <summary>
    /// Loads a material type descriptor from a JSON file and registers it into a registry.
    /// </summary>
    /// <param name="registry">The registry to populate.</param>
    /// <param name="shaderFactory">The backend shader factory.</param>
    /// <param name="path">The JSON material type definition file path.</param>
    public static void RegisterFile(IMaterialTypeRegistry registry, IMaterialShaderFactory shaderFactory, string path)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(shaderFactory);
        registry.Register(new MaterialTypeDefinition(LoadFile(path), shaderFactory));
    }

    /// <summary>
    /// Loads material type descriptors from a directory and registers them into a registry.
    /// </summary>
    /// <param name="registry">The registry to populate.</param>
    /// <param name="shaderFactory">The backend shader factory.</param>
    /// <param name="directoryPath">The directory containing material type JSON files.</param>
    public static void RegisterDirectory(IMaterialTypeRegistry registry, IMaterialShaderFactory shaderFactory, string directoryPath)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(shaderFactory);

        IReadOnlyList<MaterialTypeDefinition> definitions = CreateDefinitions(LoadDirectory(directoryPath), shaderFactory);
        for (int index = 0; index < definitions.Count; index++)
        {
            registry.Register(definitions[index]);
        }
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web)
        {
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            WriteIndented = true,
        };

        options.Converters.Add(new JsonStringEnumConverter<MaterialPipelineKind>(JsonNamingPolicy.CamelCase, false));
        options.Converters.Add(new JsonStringEnumConverter<VertexAttributeType>(JsonNamingPolicy.CamelCase, false));
        options.Converters.Add(new JsonStringEnumConverter<CullMode>(JsonNamingPolicy.CamelCase, false));
        options.Converters.Add(new JsonStringEnumConverter<FaceWinding>(JsonNamingPolicy.CamelCase, false));
        options.Converters.Add(new JsonStringEnumConverter<BlendFactor>(JsonNamingPolicy.CamelCase, false));
        options.Converters.Add(new JsonStringEnumConverter<BlendOp>(JsonNamingPolicy.CamelCase, false));
        options.Converters.Add(new JsonStringEnumConverter<CompareFunc>(JsonNamingPolicy.CamelCase, false));
        options.Converters.Add(new JsonStringEnumConverter<PrimitiveTopology>(JsonNamingPolicy.CamelCase, false));
        options.Converters.Add(new JsonStringEnumConverter<MaterialValueType>(JsonNamingPolicy.CamelCase, false));
        return options;
    }
}