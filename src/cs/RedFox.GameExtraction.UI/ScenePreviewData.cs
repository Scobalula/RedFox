using RedFox.Graphics3D;

namespace RedFox.GameExtraction.UI;

/// <summary>
/// Scene data prepared before the scene is attached to the preview UI.
/// </summary>
public sealed class ScenePreviewData
{
    /// <summary>
    /// Gets the scene bounds with and without skeleton bones.
    /// </summary>
    public ScenePreviewBounds Bounds { get; }

    /// <summary>
    /// Gets the scene nodes gathered during preparation.
    /// </summary>
    public IReadOnlyList<SceneNode> Nodes { get; }

    /// <summary>
    /// Gets the meshes gathered during preparation.
    /// </summary>
    public IReadOnlyList<Mesh> Meshes { get; }

    /// <summary>
    /// Gets the skeleton bones gathered during preparation.
    /// </summary>
    public IReadOnlyList<SkeletonBone> Bones { get; }

    /// <summary>
    /// Gets the animation clips gathered during preparation.
    /// </summary>
    public IReadOnlyList<Animation> Animations { get; }

    /// <summary>
    /// Gets the vertex count for the scene.
    /// </summary>
    public int VertexCount { get; }

    /// <summary>
    /// Gets the face count for the scene.
    /// </summary>
    public int FaceCount { get; }

    /// <summary>
    /// Gets the skeleton bone count for the scene.
    /// </summary>
    public int BoneCount { get; }

    /// <summary>
    /// Gets a value indicating whether the scene already contains a light.
    /// </summary>
    public bool HasLights { get; }

    internal ScenePreviewData(
        ScenePreviewBounds bounds,
        SceneNode[] nodes,
        Mesh[] meshes,
        SkeletonBone[] bones,
        Animation[] animations,
        int vertexCount,
        int faceCount,
        int boneCount,
        bool hasLights)
    {
        Bounds = bounds;
        Nodes = nodes;
        Meshes = meshes;
        Bones = bones;
        Animations = animations;
        VertexCount = vertexCount;
        FaceCount = faceCount;
        BoneCount = boneCount;
        HasLights = hasLights;
    }
}
