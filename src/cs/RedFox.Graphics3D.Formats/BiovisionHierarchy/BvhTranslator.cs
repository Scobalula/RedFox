using RedFox.Graphics3D.IO;

namespace RedFox.Graphics3D.Formats.BiovisionHierarchy;

/// <summary>
/// Provides reading and writing of Biovision Hierarchy (<c>.bvh</c>) animation files.
/// </summary>
public sealed class BvhTranslator : SceneTranslator
{
    /// <inheritdoc />
    public override string Name => "BiovisionHierarchy";

    /// <summary>
    /// Gets a value indicating whether this translator supports reading BVH files.
    /// </summary>
    public override bool CanRead => true;

    /// <summary>
    /// Gets a value indicating whether this translator supports writing BVH files.
    /// </summary>
    public override bool CanWrite => true;

    /// <summary>
    /// Gets the file extensions handled by this translator.
    /// </summary>
    public override IReadOnlyList<string> Extensions => [BvhFormat.Extension];

    /// <inheritdoc/>
    public override SceneTranslatorContent Content => SceneTranslatorContent.Animation;

    /// <summary>
    /// Reads BVH content from a stream and populates an existing scene instance.
    /// </summary>
    /// <param name="scene">The scene that receives parsed skeleton and animation data.</param>
    /// <param name="stream">The input stream containing BVH text data.</param>
    /// <param name="context">The translation context for this operation.</param>
    /// <param name="token">Optional cancellation token propagated by the translator pipeline.</param>
    public override void Read(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? token)
    {
        token?.ThrowIfCancellationRequested();
        BvhReader reader = new(stream, context.Name, context.Options);
        reader.Read(scene, token ?? CancellationToken.None);
    }

    /// <summary>
    /// Writes scene data to a stream as a BVH file.
    /// </summary>
    /// <param name="scene">The scene to serialize.</param>
    /// <param name="stream">The output stream that receives BVH text data.</param>
    /// <param name="context">The translation context for this operation.</param>
    /// <param name="token">Optional cancellation token propagated by the translator pipeline.</param>
    public override void Write(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? token)
    {
        token?.ThrowIfCancellationRequested();
        BvhWriter writer = new(stream, context.Name, context.Options);
        writer.Write(context.GetSelection(scene), token ?? CancellationToken.None);
    }
}
