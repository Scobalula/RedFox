using RedFox.Graphics3D.IO;

namespace RedFox.Graphics3D.Md5;

/// <summary>
/// Provides reading and writing of id Tech 4 MD5 animation (<c>.md5anim</c>) scene files.
/// <para>
/// An MD5 animation file defines a joint hierarchy with a base-frame pose and per-frame
/// animated component overrides.  Each joint has a flag bitmask selecting which of six
/// transform components (Tx, Ty, Tz, Qx, Qy, Qz) are stored in the per-frame data.
/// </para>
/// <para>
/// On reading, the translator constructs a <see cref="Skeletal.Skeleton"/> from the
/// hierarchy and a <see cref="Skeletal.SkeletonAnimation"/> with one track per joint.
/// </para>
/// <para>
/// On writing, the translator converts the animation tracks to world-space component
/// arrays and emits a valid MD5 animation file.
/// </para>
/// </summary>
public sealed class Md5AnimTranslator : SceneTranslator
{
    /// <inheritdoc/>
    public override string Name => "IdTech4MD5Anim";

    /// <summary>
    /// Gets a value indicating whether this translator supports reading MD5 anim files.
    /// </summary>
    public override bool CanRead => true;

    /// <summary>
    /// Gets a value indicating whether this translator supports writing MD5 anim files.
    /// </summary>
    public override bool CanWrite => true;

    /// <summary>
    /// Gets the file extensions handled by this translator.
    /// </summary>
    public override IReadOnlyList<string> Extensions => [".md5anim"];

    /// <summary>
    /// Reads MD5 animation content from a stream and populates an existing scene instance.
    /// </summary>
    /// <param name="scene">The scene that receives parsed skeleton and animation data.</param>
    /// <param name="stream">The input stream containing MD5 animation text data.</param>
    /// <param name="context">The translation context for this operation.</param>
    /// <param name="token">Optional cancellation token propagated by the translator pipeline.</param>
    public override void Read(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? token)
    {
        var reader = new Md5AnimReader(stream, context.Name, context.Options);
        reader.Read(scene);
    }

    /// <summary>
    /// Writes scene data to a stream as an MD5 animation file.
    /// </summary>
    /// <param name="scene">The scene to serialise.</param>
    /// <param name="stream">The output stream that receives MD5 animation text data.</param>
    /// <param name="context">The translation context for this operation.</param>
    /// <param name="token">Optional cancellation token propagated by the translator pipeline.</param>
    public override void Write(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? token)
    {
        var writer = new Md5AnimWriter(stream, context.Name, context.Options);
        writer.Write(scene);
    }
}
