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
    /// <summary>
    /// Scene translation option key that merges OBJ group/object mesh chunks into one mesh per material when set to <see langword="true"/>.
    /// </summary>
    public const string MergeStaticMeshesOption = "WavefrontObj.MergeStaticMeshes";

    /// <inheritdoc/>
    public override string Name => "WavefrontOBJ";

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanWrite => true;

    /// <inheritdoc/>
    public override IReadOnlyList<string> Extensions => [".obj"];

    /// <summary>
    /// Reads scene data from the specified OBJ stream, automatically resolving any referenced
    /// MTL material libraries relative to the source directory when file system context is available.
    /// </summary>
    /// <param name="scene">The scene to populate.</param>
    /// <param name="stream">The input stream containing OBJ data.</param>
    /// <param name="context">The translation context for this operation.</param>
    /// <param name="token">An optional cancellation token.</param>
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
    /// Writes scene data to the specified OBJ stream. When file system context is available, an
    /// accompanying .mtl file is generated for any materials referenced by the scene's meshes.
    /// </summary>
    /// <param name="scene">The scene to export.</param>
    /// <param name="stream">The output stream to write OBJ data to.</param>
    /// <param name="context">The translation context for this operation.</param>
    /// <param name="token">An optional cancellation token.</param>
    public override void Write(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? token)
    {
        string baseName = context.Name;
        string mtlFileName = $"{baseName}.mtl";
        ObjWriter writer = new(stream, baseName, context.Options);
        IReadOnlyList<Material> materials = writer.Write(context.GetSelection(scene), mtlFileName);

        if (materials.Count > 0 && !string.IsNullOrWhiteSpace(context.TargetDirectoryPath))
        {
            string mtlPath = Path.Combine(context.TargetDirectoryPath, mtlFileName);
            using FileStream mtlStream = new(mtlPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096);
            ObjMtlWriter.Write(mtlStream, materials, context.TargetDirectoryPath);
        }
    }

    private static Dictionary<string, Material> BuildMaterialDictionary(Scene scene)
    {
        Dictionary<string, Material> result = new(StringComparer.Ordinal);
        foreach (Material material in scene.EnumerateDescendants<Material>())
        {
            result.TryAdd(material.Name, material);
        }

        return result;
    }
}
