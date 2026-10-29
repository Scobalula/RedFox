using RedFox.Graphics3D.IO;

namespace RedFox.Graphics3D.Formats.StudioMDL;

/// <summary>
/// Provides reading and writing of Valve SMD (<c>.smd</c>) scene files.
/// </summary>
public sealed class SmdTranslator : SceneTranslator
{
    /// <inheritdoc/>
    public override string Name => "ValveSMD";

    /// <summary>
    /// Gets a value indicating whether this translator supports reading SMD files.
    /// </summary>
    public override bool CanRead => true;

    /// <summary>
    /// Gets a value indicating whether this translator supports writing SMD files.
    /// </summary>
    public override bool CanWrite => true;

    /// <summary>
    /// Gets the file extensions handled by this translator.
    /// </summary>
    public override IReadOnlyList<string> Extensions => [".smd"];

    /// <summary>
    /// Reads SMD content from a stream and populates an existing scene instance.
    /// </summary>
    /// <param name="scene">
    /// The scene that receives parsed skeleton, mesh, and animation data.
    /// </param>
    /// <param name="stream">
    /// The input stream containing SMD text data.
    /// </param>
    /// <param name="context">
    /// The translation context for this operation.
    /// </param>
    /// <param name="token">
    /// Optional cancellation token propagated by the translator pipeline.
    /// </param>
    public override void Read(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? token)
    {
        token?.ThrowIfCancellationRequested();
        var reader = new SmdReader(stream, context.Name, context.Options);
        reader.Read(scene, token ?? CancellationToken.None);
    }

    /// <summary>
    /// Writes scene data to a stream as an SMD file.
    /// </summary>
    /// <param name="scene">
    /// The scene to serialise.
    /// </param>
    /// <param name="stream">
    /// The output stream that receives SMD text data.
    /// </param>
    /// <param name="context">
    /// The translation context for this operation.
    /// </param>
    /// <param name="token">
    /// Optional cancellation token propagated by the translator pipeline.
    /// </param>
    public override void Write(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? token)
    {
        token?.ThrowIfCancellationRequested();
        var writer = new SmdWriter(stream, context.Name, context.Options);
        writer.Write(context.GetSelection(scene), token ?? CancellationToken.None);
    }
}
