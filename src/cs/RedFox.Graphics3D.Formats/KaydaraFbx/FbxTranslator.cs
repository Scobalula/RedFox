using RedFox.Graphics3D.IO;

namespace RedFox.Graphics3D.Formats.KaydaraFbx;

/// <summary>
/// Provides read and write translation for Kaydara FBX documents, including ASCII and binary variants.
/// </summary>
public sealed class FbxTranslator : SceneTranslator
{
    /// <summary>
    /// The <see cref="SceneTranslatorOptions"/> key that writes ASCII instead of <see cref="FbxWriteOptions.Format"/> when set to <see langword="true"/>.
    /// </summary>
    public const string WriteAsciiOption = "KaydaraFbx.WriteAscii";

    /// <inheritdoc/>
    public override string Name => "KaydaraFBX";

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanWrite => true;

    /// <inheritdoc/>
    public override IReadOnlyList<string> Extensions => [".fbx"];

    /// <inheritdoc/>
    public override ReadOnlySpan<byte> MagicValue => FbxBinarySerializer.Header[..21];

    /// <summary>
    /// Gets or sets the options used when writing FBX files.
    /// </summary>
    public FbxWriteOptions WriteOptions { get; set; } = new();

    /// <inheritdoc/>
    public override bool IsValid(string filePath, string extension, SceneTranslationContext context, ReadOnlySpan<byte> startOfFile)
    {
        return IsValid(filePath, extension, context);
    }

    /// <inheritdoc/>
    public override void Read(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? token)
    {
        FbxSceneImporter.Import(FbxDocumentIO.Read(stream), scene, context.SourceDirectoryPath);
    }

    /// <inheritdoc/>
    public override void Write(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? token)
    {
        FbxDocument document = FbxSceneExporter.Export(context.GetSelection(scene), WriteOptions, context.TargetDirectoryPath);
        FbxDocumentIO.Write(stream, document, ResolveWriteFormat(context));
    }

    /// <summary>
    /// Resolves the encoding for a write, choosing ASCII when <see cref="WriteAsciiOption"/> is set or the target name ends with <c>.ascii.fbx</c>.
    /// </summary>
    /// <param name="context">The translation context of the write.</param>
    /// <returns>The encoding to write.</returns>
    public FbxFormat ResolveWriteFormat(SceneTranslationContext context)
    {
        if (context.Options.Get<bool>(WriteAsciiOption))
            return FbxFormat.Ascii;

        if (context.Name.EndsWith(".ascii", StringComparison.OrdinalIgnoreCase) || context.TargetFilePath?.EndsWith(".ascii.fbx", StringComparison.OrdinalIgnoreCase) == true)
            return FbxFormat.Ascii;

        return WriteOptions.Format;
    }
}
