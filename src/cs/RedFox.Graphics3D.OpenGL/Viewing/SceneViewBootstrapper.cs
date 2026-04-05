using System.Numerics;
using System.Reflection;
using RedFox.Graphics3D.IO;

namespace RedFox.Graphics3D.OpenGL.Viewing;

/// <summary>
/// Provides helper methods for loading, normalizing, and preparing scenes for preview rendering.
/// </summary>
public static class SceneViewBootstrapper
{
    private static readonly FieldInfo? SceneField = typeof(SceneNode).GetField("_scene", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>
    /// Loads one or more scene files into a single preview scene, optionally normalizing the scale.
    /// </summary>
    /// <param name="filePaths">The file paths of the scenes to load.</param>
    /// <param name="translatorManager">The translator manager used to read scene files.</param>
    /// <param name="normalizeScene">Whether to normalize the scene scale to <paramref name="normalizeRadius"/>.</param>
    /// <param name="normalizeRadius">The target bounding sphere radius when normalizing.</param>
    /// <param name="upAxis">The up-axis convention to assume for the loaded scene.</param>
    /// <returns>A combined <see cref="Scene"/> containing all imported geometry.</returns>
    public static Scene LoadScene(
        IEnumerable<string> filePaths,
        SceneTranslatorManager translatorManager,
        bool normalizeScene = false,
        float normalizeRadius = 10.0f,
        SceneUpAxis upAxis = SceneUpAxis.Y)
    {
        ArgumentNullException.ThrowIfNull(filePaths);
        ArgumentNullException.ThrowIfNull(translatorManager);

        Scene scene = new("Preview Scene");

        foreach (string inputPath in filePaths)
        {
            string fullPath = Path.GetFullPath(inputPath);
            Scene importedScene = translatorManager.Read(fullPath, new SceneTranslatorOptions(), token: null);
            Model container = scene.RootNode.AddNode(new Model
            {
                Name = GetUniqueChildName(scene.RootNode, Path.GetFileNameWithoutExtension(fullPath)),
            });

            foreach (SceneNode child in importedScene.RootNode.EnumerateChildren().ToArray())
            {
                child.Detach();
                container.AddNode(child);
                AssignSceneRecursive(child, scene);
            }
        }

        EnsureMorphTargets(scene);

        if (normalizeScene)
            NormalizeSceneScale(scene, normalizeRadius);

        return scene;
    }

    /// <summary>
    /// Computes the combined scene transform from the up-axis convention and optional normalization scaling.
    /// </summary>
    /// <param name="scene">The scene used to determine the current bounding radius for normalization.</param>
    /// <param name="upAxis">The up-axis convention for the scene.</param>
    /// <param name="normalizeScene">Whether to apply normalization scaling.</param>
    /// <param name="normalizeRadius">The target bounding sphere radius when normalizing.</param>
    /// <returns>The combined scene transform matrix.</returns>
    public static Matrix4x4 GetSceneTransform(Scene? scene, SceneUpAxis upAxis, bool normalizeScene, float normalizeRadius)
    {
        Matrix4x4 transform = GetUpAxisTransform(upAxis);

        if (!normalizeScene || scene is null || !SceneBounds.TryGetBounds(scene, out SceneBoundsInfo bounds) || bounds.Radius <= 1e-6f)
            return transform;

        float scale = normalizeRadius / bounds.Radius;
        if (!float.IsFinite(scale) || scale <= 0.0f)
            return transform;

        return Matrix4x4.CreateScale(scale) * transform;
    }

    /// <summary>
    /// Returns the rotation matrix that converts coordinates from the specified up-axis convention to Y-up.
    /// </summary>
    /// <param name="upAxis">The source up-axis convention.</param>
    /// <returns>A rotation matrix that reorients the scene to Y-up.</returns>
    public static Matrix4x4 GetUpAxisTransform(SceneUpAxis upAxis)
    {
        Quaternion rotation = upAxis switch
        {
            SceneUpAxis.Y => Quaternion.Identity,
            SceneUpAxis.Z => Quaternion.CreateFromAxisAngle(Vector3.UnitX, -MathF.PI * 0.5f),
            SceneUpAxis.X => Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI * 0.5f),
            _ => Quaternion.Identity,
        };

        return Matrix4x4.CreateFromQuaternion(rotation);
    }

    /// <summary>
    /// Determines whether a scene node has any explicit transform data set in its bind or live transforms.
    /// </summary>
    /// <param name="node">The scene node to inspect.</param>
    /// <returns><c>true</c> if the node or any of its transforms contain explicit values.</returns>
    public static bool HasExplicitTransform(SceneNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        return node.Parent is not null
            || node.BindTransform.LocalPosition.HasValue
            || node.BindTransform.WorldPosition.HasValue
            || node.BindTransform.LocalRotation.HasValue
            || node.BindTransform.WorldRotation.HasValue
            || node.BindTransform.Scale.HasValue
            || node.LiveTransform.LocalPosition.HasValue
            || node.LiveTransform.WorldPosition.HasValue
            || node.LiveTransform.LocalRotation.HasValue
            || node.LiveTransform.WorldRotation.HasValue
            || node.LiveTransform.Scale.HasValue;
    }

    /// <summary>
    /// Updates the camera's position, target, and up vector from its current world matrix.
    /// </summary>
    /// <param name="camera">The camera to synchronize.</param>
    public static void SynchronizeCameraFromNodeTransform(Camera camera)
    {
        ArgumentNullException.ThrowIfNull(camera);

        Matrix4x4 world = camera.GetActiveWorldMatrix();
        if (!Matrix4x4.Decompose(world, out _, out Quaternion rotation, out Vector3 translation))
            return;

        Vector3 forward = Vector3.Normalize(Vector3.Transform(-Vector3.UnitZ, rotation));
        Vector3 up = Vector3.Normalize(Vector3.Transform(Vector3.UnitY, rotation));

        if (!IsFinite(forward) || !IsFinite(up))
            return;

        camera.Position = translation;
        camera.Target = translation + forward;
        camera.Up = up;
    }

    private static void EnsureMorphTargets(Scene scene)
    {
        foreach (Mesh mesh in scene.RootNode.EnumerateDescendants<Mesh>())
        {
            if (!mesh.HasMorphTargets)
                continue;

            HashSet<int> existingTargets = mesh.EnumerateDescendants<BlendShape>()
                .Select(shape => shape.TargetIndex)
                .ToHashSet();

            for (int targetIndex = 0; targetIndex < mesh.MorphTargetCount; targetIndex++)
            {
                if (existingTargets.Contains(targetIndex))
                    continue;

                mesh.AddNode(new BlendShape($"Target{targetIndex}", targetIndex, mesh));
            }
        }
    }

    private static void NormalizeSceneScale(Scene scene, float targetRadius)
    {
        if (!SceneBounds.TryGetBounds(scene, out SceneBoundsInfo bounds) || bounds.Radius <= 1e-6f)
            return;

        float scale = targetRadius / bounds.Radius;
        if (!float.IsFinite(scale) || scale <= 0f || MathF.Abs(scale - 1.0f) < 1e-4f)
            return;

        foreach (SceneNode child in scene.RootNode.EnumerateChildren())
        {
            Vector3 existingScale = child.BindTransform.Scale ?? Vector3.One;
            child.BindTransform.Scale = existingScale * scale;

            if (child.BindTransform.LocalPosition.HasValue)
                child.BindTransform.LocalPosition *= scale;

            if (child.BindTransform.WorldPosition.HasValue)
                child.BindTransform.WorldPosition *= scale;
        }
    }

    private static void AssignSceneRecursive(SceneNode node, Scene scene)
    {
        if (SceneField is not null)
            SceneField.SetValue(node, scene);

        foreach (SceneNode child in node.EnumerateChildren())
            AssignSceneRecursive(child, scene);
    }

    private static string GetUniqueChildName(SceneNode parent, string baseName)
    {
        string sanitizedBaseName = string.IsNullOrWhiteSpace(baseName) ? "Scene" : baseName;
        string candidate = sanitizedBaseName;
        int suffix = 1;

        while (parent.EnumerateChildren().Any(node => node.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
            candidate = $"{sanitizedBaseName}_{suffix++}";

        return candidate;
    }

    private static bool IsFinite(Vector3 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    }
}
