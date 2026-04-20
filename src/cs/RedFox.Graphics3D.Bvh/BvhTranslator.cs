using RedFox.Graphics3D.IO;

namespace RedFox.Graphics3D.Bvh;

/// <summary>
/// Provides reading and writing of Biovision Hierarchy (<c>.bvh</c>) animation files.
/// </summary>
/// <para>
/// BVH files encode one skeleton hierarchy, bind-pose offsets, and a single motion clip composed of per-joint
/// translation and rotation channels. They do not encode mesh geometry, normals, tangents, UVs, materials, or
/// skinning weights.
/// </para>
/// <para>
/// On import, the translator creates a standard <see cref="Skeleton"/> composed of existing <see cref="SkeletonBone"/>
/// nodes and a <see cref="Skeletal.SkeletonAnimation"/> that stores the motion clip in local absolute space.
/// </para>
/// <para>
/// On export, the translator validates that the scene only contains data representable by BVH. Unsupported scene
/// content is rejected explicitly instead of being silently discarded, and the writer emits a stable default BVH
/// channel order rather than coupling the scene graph to format-specific node types.
/// </para>
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

    /// <summary>
    /// Reads BVH content from a stream and populates an existing scene instance.
    /// </summary>
    /// <param name="scene">The scene that receives parsed skeleton and animation data.</param>
    /// <param name="stream">The input stream containing BVH text data.</param>
    /// <param name="context">The translation context for this operation.</param>
    /// <param name="token">Optional cancellation token propagated by the translator pipeline.</param>
    public override void Read(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? token)
    {
        BvhReader reader = new(stream, context.Name, context.Options);
        reader.Read(scene);
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
        BvhWriter writer = new(stream, context.Name, context.Options);
        writer.Write(context.GetSelection(scene));
    }
}
