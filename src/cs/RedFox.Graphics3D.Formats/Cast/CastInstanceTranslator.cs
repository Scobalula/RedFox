using CastNet.Nodes;
using System.Numerics;

namespace RedFox.Graphics3D.Formats.Cast;

internal static class CastInstanceTranslator
{
    public static void Read(Scene scene, InstanceNode instanceNode, string? sceneRoot)
    {
        if (instanceNode.ReferenceFile is not FileNode { Path.Length: > 0 } file)
            return;

        var name = CastTranslator.GetUniqueName(scene.RootNode, instanceNode.Name ?? Path.GetFileNameWithoutExtension(file.Path));
        var reference = scene.RootNode.AddNode(new SceneReference(name, file.Path));

        reference.ResolvedFilePath = FilePathResolver.ResolveReferencePath(file.Path, sceneRoot);
        reference.BindTransform.LocalPosition = instanceNode.Position;
        reference.BindTransform.LocalRotation = instanceNode.Rotation;
        reference.BindTransform.Scale = instanceNode.Scale;
    }

    public static void Write(RootNode root, SceneReference reference, string? targetDirectory)
    {
        var path = reference.ResolvedFilePath is string resolved && targetDirectory is not null ? Path.GetRelativePath(targetDirectory, resolved) : reference.FilePath;

        root.AddNode(new InstanceNode
        {
            Name = reference.Name,
            ReferenceFile = new FileNode { Path = path },
            Position = reference.BindTransform.LocalPosition ?? Vector3.Zero,
            Rotation = reference.BindTransform.LocalRotation ?? Quaternion.Identity,
            Scale = reference.BindTransform.Scale ?? Vector3.One,
        });
    }
}
