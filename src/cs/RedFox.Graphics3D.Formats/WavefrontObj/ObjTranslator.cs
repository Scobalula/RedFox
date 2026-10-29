using RedFox.Graphics3D.IO;

namespace RedFox.Graphics3D.Formats.WavefrontObj;

/// <summary>
/// Provides reading and writing of Wavefront OBJ (.obj) scene files with accompanying MTL (.mtl) material libraries.
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
    /// Reads scene data from the specified OBJ stream, automatically resolving any referenced MTL material libraries relative to the source directory when file system context is available.
    /// </summary>
    /// <param name="scene">The scene to populate.</param>
    /// <param name="stream">The input stream containing OBJ data.</param>
    /// <param name="context">The translation context for this operation.</param>
    /// <param name="token">An optional cancellation token.</param>
    public override void Read(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? token)
    {
        token?.ThrowIfCancellationRequested();
        ObjReader reader				= new(stream, context.Name, context.Options);
        IReadOnlyList<string> mtlPaths	= reader.Read(scene, token ?? CancellationToken.None);

        string? baseDir = context.SourceDirectoryPath;
        if (baseDir is not null)
        {
            Dictionary<string, Material> existingMaterials = BuildMaterialDictionary(scene);
            foreach (string mtlRelativePath in mtlPaths)
            {
                if (FilePathResolver.ResolveReferencePath(mtlRelativePath, baseDir) is string mtlFullPath && File.Exists(mtlFullPath))
                {
                    using FileStream mtlStream = new(mtlFullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
                    ObjMtlReader.Read(mtlStream, existingMaterials, Path.GetDirectoryName(mtlFullPath));
                }
            }
        }
    }

    /// <summary>
    /// Writes scene data to the specified OBJ stream.
    /// </summary>
    /// <param name="scene">The scene to export.</param>
    /// <param name="stream">The output stream to write OBJ data to.</param>
    /// <param name="context">The translation context for this operation.</param>
    /// <param name="token">An optional cancellation token.</param>
    public override void Write(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? token)
    {
        token?.ThrowIfCancellationRequested();
        string baseName						= context.Name;
        string mtlFileName					= $"{baseName}.mtl";
        ObjWriter writer					= new(stream, baseName, context.Options);
        IReadOnlyList<Material> materials	= writer.Write(context.GetSelection(scene), mtlFileName, token ?? CancellationToken.None);

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
