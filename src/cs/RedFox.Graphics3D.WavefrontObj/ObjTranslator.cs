using RedFox.Graphics3D.IO;

namespace RedFox.Graphics3D.WavefrontObj;

/// <summary>
/// Provides reading and writing of Wavefront OBJ (.obj) scene files with accompanying
/// MTL (.mtl) material libraries. Supports triangle meshes with positions, normals,
/// texture coordinates, object groups, and material references.
/// <para>
/// When reading from a file path, referenced MTL libraries are automatically resolved
/// relative to the OBJ file's directory. When reading from a raw stream, only the OBJ
/// geometry is parsed; MTL files cannot be resolved without file system context.
/// </para>
/// <para>
/// When writing to a file path, an accompanying .mtl file is automatically generated
/// alongside the .obj file for any materials referenced by the scene's meshes.
/// When writing to a raw stream, only the OBJ geometry is emitted with a <c>mtllib</c>
/// directive; the caller is responsible for writing the MTL data separately.
/// </para>
/// </summary>
public sealed class ObjTranslator : SceneTranslator
{
    /// <inheritdoc/>
    public override string Name => "WavefrontOBJ";

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanWrite => true;

    /// <inheritdoc/>
    public override IReadOnlyList<string> Extensions => [".obj"];

    /// <summary>
    /// Reads scene data from the specified OBJ file, automatically resolving any referenced
    /// MTL material libraries relative to the OBJ file's directory.
    /// </summary>
    /// <param name="scene">The scene to populate.</param>
    /// <param name="filePath">The path to the OBJ file.</param>
    /// <param name="options">Options that control how the scene data is read.</param>
    /// <param name="token">An optional cancellation token.</param>
    public override void Read(Scene scene, string filePath, SceneTranslatorOptions options, CancellationToken? token)
    {
        using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
        Read(scene, stream, CreateReadContext(filePath, options), token);
    }

    public override void Read(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? token)
    {
        ObjReader reader = new(stream, context.Name, context.Options);
        IReadOnlyList<string> mtlPaths = reader.Read(scene);

        // Resolve MTL files relative to the OBJ file's directory.
        string? baseDir = context.SourceDirectoryPath;
        if (baseDir is not null)
        {
            Dictionary<string, Material> existingMaterials = BuildMaterialDictionary(scene);
            foreach (string mtlRelativePath in mtlPaths)
            {
                string mtlFullPath = Path.GetFullPath(Path.Combine(baseDir, mtlRelativePath));
                if (File.Exists(mtlFullPath))
                {
                    using FileStream mtlStream = new(mtlFullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
                    ObjMtlReader.Read(mtlStream, existingMaterials, baseDir);
                }
            }
        }
    }

    /// <summary>
    /// Reads scene data from the specified OBJ stream. MTL material libraries cannot be
    /// resolved without file system context and are not loaded in this overload.
    /// </summary>
    /// <param name="scene">The scene to populate.</param>
    /// <param name="stream">The input stream containing OBJ data.</param>
    /// <param name="name">The file or scene name used for the root model node.</param>
    /// <param name="options">Options that control how the scene data is read.</param>
    /// <param name="token">An optional cancellation token.</param>
    public override void Read(Scene scene, Stream stream, string name, SceneTranslatorOptions options, CancellationToken? token)
    {
        Read(scene, stream, new SceneTranslationContext(name, options)
        {
            SourceDirectoryPath = options.SourceDirectoryPath,
            SourceFilePath = options.SourceFilePath,
        }, token);
    }

    /// <summary>
    /// Writes scene data to the specified OBJ file. An accompanying .mtl file is automatically
    /// generated in the same directory for any materials referenced by the scene's meshes.
    /// </summary>
    /// <param name="scene">The scene to export.</param>
    /// <param name="filePath">The output OBJ file path.</param>
    /// <param name="options">Options that control how the scene data is written.</param>
    /// <param name="token">An optional cancellation token.</param>
    public override void Write(Scene scene, string filePath, SceneTranslatorOptions options, CancellationToken? token)
    {
        using FileStream stream = new(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096);
        Write(scene, stream, CreateWriteContext(filePath, options), token);
    }

    public override void Write(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? token)
    {
        string baseName = context.Name;
        string mtlFileName = $"{baseName}.mtl";
        ObjWriter writer = new(stream, baseName, context.Options);
        IReadOnlyList<Material> materials = writer.Write(scene, mtlFileName);

        if (materials.Count > 0 && !string.IsNullOrWhiteSpace(context.TargetDirectoryPath))
        {
            string mtlPath = Path.Combine(context.TargetDirectoryPath, mtlFileName);
            using FileStream mtlStream = new(mtlPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096);
            ObjMtlWriter.Write(mtlStream, materials);
        }
    }

    /// <summary>
    /// Writes scene data to the specified OBJ stream. Only the OBJ geometry and a mtllib
    /// directive are emitted; the MTL data must be written separately by the caller.
    /// </summary>
    /// <param name="scene">The scene to export.</param>
    /// <param name="stream">The output stream to write OBJ data to.</param>
    /// <param name="name">The scene or file name used in the OBJ header.</param>
    /// <param name="options">Options that control how the scene data is written.</param>
    /// <param name="token">An optional cancellation token.</param>
    public override void Write(Scene scene, Stream stream, string name, SceneTranslatorOptions options, CancellationToken? token)
    {
        Write(scene, stream, new SceneTranslationContext(name, options), token);
    }

    private static Dictionary<string, Material> BuildMaterialDictionary(Scene scene)
    {
        Dictionary<string, Material> result = new(StringComparer.Ordinal);
        foreach (Material material in scene.EnumerateDescendants<Material>(SceneNodeFlags.NoExport))
        {
            result.TryAdd(material.Name, material);
        }

        return result;
    }
}
