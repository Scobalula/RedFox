using RedFox.Graphics3D.IO;

namespace RedFox.Graphics3D.Md5;

/// <summary>
/// Provides reading and writing of id Tech 4 MD5 mesh (<c>.md5mesh</c>) scene files.
/// <para>
/// An MD5 mesh file contains a skeleton hierarchy with object-space bind-pose transforms
/// and one or more skinned meshes.  Each mesh defines vertices via a weight table that
/// binds geometry to the skeleton joints.
/// </para>
/// <para>
/// On reading, the translator parses the joint hierarchy, creates a <see cref="Skeletal.Skeleton"/>
/// and a <see cref="Model"/> containing one <see cref="Mesh"/> per sub-mesh block.  Vertex
/// positions, normals, and tangents are computed from the weight data.
/// </para>
/// <para>
/// On writing, the translator decomposes skinned vertex positions back into joint-local
/// weight offsets and emits a valid MD5 mesh file.
/// </para>
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
    /// <param name="name">The logical scene name or source file name.</param>
    /// <param name="options">Translator options that control read behaviour.</param>
    /// <param name="token">Optional cancellation token propagated by the translator pipeline.</param>
    public override void Read(Scene scene, Stream stream, string name, SceneTranslatorOptions options, CancellationToken? token)
    {
        var reader = new Md5MeshReader(stream, name, options);
        reader.Read(scene);
    }

    /// <summary>
    /// Writes scene data to a stream as an MD5 mesh file.
    /// </summary>
    /// <param name="scene">The scene to serialise.</param>
    /// <param name="stream">The output stream that receives MD5 mesh text data.</param>
    /// <param name="name">The logical scene name or destination file name.</param>
    /// <param name="options">Translator options that control write behaviour.</param>
    /// <param name="token">Optional cancellation token propagated by the translator pipeline.</param>
    public override void Write(Scene scene, Stream stream, string name, SceneTranslatorOptions options, CancellationToken? token)
    {
        var writer = new Md5MeshWriter(stream, name, options);
        writer.Write(scene);
    }
}
