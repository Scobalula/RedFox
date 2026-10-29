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
        token?.ThrowIfCancellationRequested();
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
        token?.ThrowIfCancellationRequested();
        var root							= new RootNode();
        SceneTranslationSelection selection	= context.GetSelection(scene);

        string upAxis = selection.Scene.UpAxis switch
        {
            SceneUpAxis.X => "x",
            SceneUpAxis.Z => "z",
            _             => "y",
        };
        root.AddNode(new MetadataNode
        {
            UpAxis = upAxis,
            // TODO: Change this, tbh could do with a refactor to how we pass "shared" settings to these? let apps set their own stuff
            Software = "RedFox Library via Cast.NET"/*Assembly.GetEntryAssembly()?.GetName().Name*/,
        });

        foreach (var (model, content) in GetExportModels(selection))
            CastModelTranslator.Write(root, model, content, selection, context.TargetDirectoryPath);

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
            "absolute"  => TransformType.Absolute,
            "relative"  => TransformType.Relative,
            "additive"  => TransformType.Additive,
            _			=> TransformType.Unknown,
        };
    }

    internal static string ConvertFromTransformType(TransformType type)
    {
        return type switch
        {
            TransformType.Absolute  => "absolute",
            TransformType.Additive  => "additive",
            _						=> "relative",
        };
    }

    internal static string GetUniqueName(SceneNode parent, string name)
    {
        string candidate = name;

        for (int index = 1; parent.TryFindChild(candidate, StringComparison.OrdinalIgnoreCase, out _); index++)
            candidate = $"{name}_{index}";

        return candidate;
    }

    private static Dictionary<SceneNode, List<SceneNode>> GetExportModels(SceneTranslationSelection selection)
    {
        Dictionary<SceneNode, List<SceneNode>> models = [];
        foreach (SceneNode node in selection.EnumerateDescendants<SceneNode>())
        {
            if (node is not (Mesh or Material or SkeletonBone or Hair or ConstraintNode or IKHandleNode))
                continue;
            SceneNode owner = selection.Scene;
            for (SceneNode? ancestor = node.Parent; ancestor is not null; ancestor = ancestor.Parent)
            {
                if (ancestor is MeshGroup)
                {
                    owner = ancestor;
                    break;
                }
            }
            if (!models.TryGetValue(owner, out List<SceneNode>? content))
            {
                content = [];
                models.Add(owner, content);
            }
            content.Add(node);
        }
        return models;
    }
}
