using RedFox.Graphics3D.Groups;
using RedFox.Graphics3D.IO;

namespace RedFox.Graphics3D.Formats.IdTech;

/// <summary>
/// Provides reading and writing of id Tech 4 MD5 mesh (<c>.md5mesh</c>) scene files.
/// </summary>
public sealed class Md5MeshTranslator : SceneTranslator
{
    /// <inheritdoc/>
    public override string Name => "IdTech4MD5Mesh";

    /// <summary>
    /// Gets a value indicating whether this translator supports reading MD5 mesh files.
    /// </summary>
    public override bool CanRead => true;

    /// <summary>
    /// Gets a value indicating whether this translator supports writing MD5 mesh files.
    /// </summary>
    public override bool CanWrite => true;

    /// <summary>
    /// Gets the file extensions handled by this translator.
    /// </summary>
    public override IReadOnlyList<string> Extensions => [".md5mesh"];

    /// <summary>
    /// Reads MD5 mesh content from a stream and populates an existing scene instance.
    /// </summary>
    /// <param name="scene">The scene that receives parsed skeleton and mesh data.</param>
    /// <param name="stream">The input stream containing MD5 mesh text data.</param>
    /// <param name="context">The translation context for this operation.</param>
    /// <param name="token">Optional cancellation token propagated by the translator pipeline.</param>
    public override void Read(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? token)
    {
        token?.ThrowIfCancellationRequested();
        var reader = new Md5MeshReader(stream, context.Name, context.Options);
        reader.Read(scene, token ?? CancellationToken.None);
    }

    /// <summary>
    /// Writes scene data to a stream as an MD5 mesh file.
    /// </summary>
    /// <param name="scene">The scene to serialise.</param>
    /// <param name="stream">The output stream that receives MD5 mesh text data.</param>
    /// <param name="context">The translation context for this operation.</param>
    /// <param name="token">Optional cancellation token propagated by the translator pipeline.</param>
    public override void Write(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? token)
    {
        token?.ThrowIfCancellationRequested();
        var writer = new Md5MeshWriter(stream, context.Name, context.Options);
        writer.Write(context.GetSelection(scene), token ?? CancellationToken.None);
    }
}
