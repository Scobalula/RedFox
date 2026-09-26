using CastNet;
using CastNet.Nodes;
using RedFox.Graphics3D.Groups;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Skeletal;
using System.Reflection;

namespace RedFox.Graphics3D.Formats.Cast;

/// <summary>
/// Provides functionality to read and write scenes in the Cast file format.
/// </summary>
public sealed class CastTranslator : SceneTranslator
{
    /// <inheritdoc/>
    public override string Name => "Cast";

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanWrite => true;

    /// <inheritdoc/>
    public override IReadOnlyList<string> Extensions => [".cast"];

    /// <inheritdoc/>
    public override void Read(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? token)
    {
        foreach (var root in CastReader.Load(stream).Roots)
        {
            if (root.Metadata?.UpAxis is string upAxis)
                scene.UpAxis = upAxis switch { "x" => SceneUpAxis.X, "z" => SceneUpAxis.Z, _ => SceneUpAxis.Y };

            foreach (var modelNode in root.EnumerateModels())
                CastModelTranslator.Read(scene, modelNode, GetUniqueName(scene.RootNode, context.Name), context.SourceDirectoryPath);

            foreach (var animationNode in root.EnumerateAnimations())
                CastAnimationTranslator.Read(scene, animationNode, GetUniqueName(scene.RootNode, context.Name));

            foreach (var instanceNode in root.EnumerateInstances())
                CastInstanceTranslator.Read(scene, instanceNode, root.Metadata?.SceneRoot ?? context.SourceDirectoryPath);
        }
    }

    /// <inheritdoc/>
    public override void Write(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? token)
    {
        var root = new RootNode();
        SceneTranslationSelection selection = context.GetSelection(scene);

        root.AddNode(new MetadataNode { UpAxis = "z", Software = Assembly.GetEntryAssembly()?.GetName().Name });

        foreach (var model in GetExportModels(selection))
            CastModelTranslator.Write(root, model, selection, context.TargetDirectoryPath);

        foreach (var animation in selection.GetDescendants<SkeletonAnimation>())
            CastAnimationTranslator.Write(root, animation);

        foreach (var animation in selection.GetDescendants<MorphAnimation>())
            CastAnimationTranslator.Write(root, animation);

        foreach (var reference in selection.GetDescendants<SceneReference>())
            CastInstanceTranslator.Write(root, reference, context.TargetDirectoryPath);

        CastWriter.Save(stream, root);
    }

    internal static TransformType ConvertToTransformType(string? transformType)
    {
        return transformType switch
        {
            "absolute" => TransformType.Absolute,
            "relative" => TransformType.Relative,
            "additive" => TransformType.Additive,
            _          => TransformType.Unknown,
        };
    }

    internal static string ConvertFromTransformType(TransformType type)
    {
        return type switch
        {
            TransformType.Absolute => "absolute",
            TransformType.Additive => "additive",
            _                      => "relative",
        };
    }

    private static IReadOnlyList<SceneNode> GetExportModels(SceneTranslationSelection selection)
    {
        // Without any mesh groups the scene itself is the model, unless it holds no model data at all (e.g. animation only scenes).
        if (selection.GetDescendants<MeshGroup>().Length == 0)
            return selection.GetDescendants<Mesh>().Length > 0 || selection.GetDescendants<SkeletonBone>().Length > 0 || selection.GetDescendants<Material>().Length > 0 ? [selection.Scene] : [];

        var meshModels = selection.GetDescendants<Mesh>().Select(mesh => mesh.EnumerateAncestors<MeshGroup>().FirstOrDefault());
        var materialModels = selection.GetDescendants<Material>().Select(material => material.EnumerateAncestors<MeshGroup>().FirstOrDefault());

        return [.. selection.GetDescendants<MeshGroup>().Concat(meshModels).Concat(materialModels).OfType<MeshGroup>().Distinct()];
    }

    internal static string GetUniqueName(SceneNode parent, string name)
    {
        string candidate = name;

        for (int index = 1; parent.TryFindChild(candidate, StringComparison.CurrentCultureIgnoreCase, out _); index++)
            candidate = $"{name}_{index}";

        return candidate;
    }
}
