using RedFox.Graphics3D.IO;

namespace RedFox.Graphics3D.KaydaraFbx;

/// <summary>
/// Provides read and write translation for Kaydara FBX documents, including ASCII and binary variants.
/// </summary>
public sealed class FbxTranslator : SceneTranslator
{
    /// <inheritdoc/>
    public override string Name => "KaydaraFBX";

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanWrite => true;

    /// <inheritdoc/>
    public override IReadOnlyList<string> Extensions => [".fbx"];

    /// <inheritdoc/>
    public override void Read(Scene scene, Stream stream, string name, SceneTranslatorOptions options, CancellationToken? token)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(stream);

        FbxDocument document = FbxDocumentIO.Read(stream);
        Scene translatedScene = FbxSceneMapper.ImportScene(document, name);

        MergeScene(scene, translatedScene);
    }

    /// <inheritdoc/>
    public override void Write(Scene scene, Stream stream, string name, SceneTranslatorOptions options, CancellationToken? token)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(stream);

        FbxFormat targetFormat = ResolveWriteFormat(name, stream);
        FbxDocument document = FbxSceneMapper.ExportScene(scene, targetFormat);
        FbxDocumentIO.Write(stream, document, targetFormat);
    }

    /// <summary>
    /// Resolves the target FBX format for the current write operation.
    /// </summary>
    /// <param name="name">The source file name or path used for export.</param>
    /// <param name="stream">The output stream that receives the encoded FBX data.</param>
    /// <returns>The selected <see cref="FbxFormat"/>.</returns>
    public static FbxFormat ResolveWriteFormat(string name, Stream stream)
    {
        _ = stream;

        if (name.EndsWith(".ascii.fbx", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".fbxascii", StringComparison.OrdinalIgnoreCase))
        {
            return FbxFormat.Ascii;
        }

        return FbxFormat.Binary;
    }

    /// <summary>
    /// Appends translated nodes from one scene into another scene instance.
    /// </summary>
    /// <param name="target">The destination scene to populate.</param>
    /// <param name="source">The temporary translated scene.</param>
    public static void MergeScene(Scene target, Scene source)
    {
        foreach (SceneNode child in source.RootNode.EnumerateChildren().ToArray())
        {
            child.MoveTo(target.RootNode, ReparentTransformMode.PreserveExisting);
        }
    }
}
