using System.Numerics;
using System.Reflection;
using RedFox.Graphics3D.IO;

namespace RedFox.Graphics3D.Preview;

public static class SceneBootstrapper
{
    private static readonly FieldInfo? SceneField = typeof(SceneNode).GetField("_scene", BindingFlags.Instance | BindingFlags.NonPublic);

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
            Model container = scene.RootNode.AddNode(new Model { Name = GetUniqueChildName(scene.RootNode, Path.GetFileNameWithoutExtension(fullPath)) });

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
        string candidate = string.IsNullOrWhiteSpace(baseName) ? "Scene" : baseName;
        int suffix = 1;

        while (parent.EnumerateChildren().Any(node => node.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
            candidate = $"{baseName}_{suffix++}";

        return candidate;
    }

    private static bool IsFinite(Vector3 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    }
}
