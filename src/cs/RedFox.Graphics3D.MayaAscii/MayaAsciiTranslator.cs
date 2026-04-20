using RedFox.Graphics3D.IO;

namespace RedFox.Graphics3D.MayaAscii;

/// <summary>
/// Provides write-only translation for Maya ASCII (.ma) scene files.
/// Supports exporting meshes, skeletons, materials, skinning, and skeletal animations
/// in a format compatible with Autodesk Maya 2012 and later.
/// <para>
/// This translator does not support reading Maya ASCII files.
/// Use <see cref="CanRead"/> to verify read capability before calling <see cref="Read"/>.
/// </para>
/// </summary>
public sealed class MayaAsciiTranslator : SceneTranslator
{
    /// <summary>
    /// Gets the write options that control how the scene is exported to the Maya ASCII format.
    /// These can be modified before calling <see cref="Write"/> to affect output behavior.
    /// </summary>
    public MayaAsciiWriteOptions WriteOptions { get; set; } = new();

    /// <inheritdoc/>
    public override string Name => "MayaASCII";

    /// <inheritdoc/>
    public override bool CanRead => false;

    /// <inheritdoc/>
    public override bool CanWrite => true;

    /// <inheritdoc/>
    public override IReadOnlyList<string> Extensions => [".ma"];

    /// <summary>
    /// Throws <see cref="NotSupportedException"/> because this translator does not support reading Maya ASCII files.
    /// </summary>
    /// <param name="scene">Unused.</param>
    /// <param name="stream">Unused.</param>
    /// <param name="context">Unused.</param>
    /// <param name="token">Unused.</param>
    /// <exception cref="NotSupportedException">Always thrown. This translator is write-only.</exception>
    public override void Read(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? token) =>
        throw new NotSupportedException("MayaAsciiTranslator does not support reading Maya ASCII files.");

    /// <summary>
    /// Writes the specified scene to the output stream as a Maya ASCII (.ma) file.
    /// The output is compatible with Autodesk Maya 2012 and later versions.
    /// </summary>
    /// <param name="scene">The scene to export. Must not be <see langword="null"/>.</param>
    /// <param name="stream">The output stream to write the Maya ASCII data to. Must be writable.</param>
    /// <param name="context">The translation context. The <see cref="SceneTranslatorOptions.WriteRawVertices"/> setting is forwarded to the writer.</param>
    /// <param name="token">An optional cancellation token. Currently unused but reserved for future support.</param>
    public override void Write(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? token)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(stream);

        MayaAsciiWriteOptions writeOptions = WriteOptions;
        writeOptions.WriteRawVertices = context.Options.WriteRawVertices;

        MayaAsciiWriter writer = new(stream, writeOptions);
        writer.Write(context.GetSelection(scene), context.Name);
    }
}
