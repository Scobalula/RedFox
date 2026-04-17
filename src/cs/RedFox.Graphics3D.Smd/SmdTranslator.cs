using RedFox.Graphics3D.IO;

namespace RedFox.Graphics3D.Smd;

/// <summary>
/// Provides reading and writing of Valve SMD (<c>.smd</c>) scene files.
/// <para>
/// Two SMD sub-formats are supported:
/// </para>
/// <list type="bullet">
///   <item>
///     <description>
///       <b>Reference SMD</b> — contains a skeleton bind pose and triangle mesh data.
///       When a scene contains <see cref="Mesh"/> nodes the writer produces this format
///       (a <c>nodes</c> section, a single <c>time 0</c> skeleton section, and a
///       <c>triangles</c> section).
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Sequence (animation) SMD</b> — contains only a skeleton and per-frame bone
///       transforms.  When a scene contains a <see cref="SkeletonAnimation"/> but no meshes
///       the writer produces this format.
///     </description>
///   </item>
/// </list>
/// <para>
/// On reading, the translator detects the sub-format automatically: a file with a
/// <c>triangles</c> section is treated as a reference SMD while a file whose
/// <c>skeleton</c> section contains more than one <c>time</c> frame is treated as a
/// sequence SMD (both may also be present in the same file).
/// </para>
/// </summary>
public sealed class SmdTranslator : SceneTranslator
{
    /// </inheritdoc>
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
        var reader = new SmdReader(stream, context.Name, context.Options);
        reader.Read(scene);
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
        var writer = new SmdWriter(stream, context.Name, context.Options);
        writer.Write(scene);
    }
}
