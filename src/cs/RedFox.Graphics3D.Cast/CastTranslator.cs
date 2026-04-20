using Cast.NET;
using Cast.NET.Nodes;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Skeletal;
using System.Reflection;

namespace RedFox.Graphics3D.Cast;

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
        var cast = CastReader.Load(stream);
        var root = cast.RootNodes[0];

        foreach (var modelNode in root.EnumerateChildrenOfType<ModelNode>())
        {
            CastModelTranslator.Read(scene, modelNode, context.Name);
        }

        foreach (var animationNode in root.EnumerateChildrenOfType<AnimationNode>())
        {
            CastAnimationTranslator.Read(scene, animationNode, context.Name);
        }
    }

    /// <inheritdoc/>
    public override void Write(Scene scene, Stream stream, SceneTranslationContext context, CancellationToken? token)
    {
        var root = new CastNode(CastNodeIdentifier.Root);
        SceneTranslationSelection selection = context.GetSelection(scene);

        var metaDataNode = root.AddNode<MetadataNode>();
        metaDataNode.AddString("up", "z");
        metaDataNode.AddString("s", Assembly.GetEntryAssembly()?.GetName().Name ?? string.Empty);

        foreach (var model in GetExportModels(selection))
        {
            CastModelTranslator.Write(root, model, selection);
        }

        foreach (var animation in selection.GetDescendants<SkeletonAnimation>())
        {
            CastAnimationTranslator.Write(root, animation);
        }

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

    private static IReadOnlyList<Model> GetExportModels(SceneTranslationSelection selection)
    {
        List<Model> models = [];
        HashSet<Model> seen = [];

        static void AddModel(List<Model> models, HashSet<Model> seen, Model? model)
        {
            if (model is not null && seen.Add(model))
                models.Add(model);
        }

        foreach (Model model in selection.GetDescendants<Model>())
            AddModel(models, seen, model);

        foreach (Mesh mesh in selection.GetDescendants<Mesh>())
            AddModel(models, seen, mesh.EnumerateAncestors<Model>().FirstOrDefault());

        foreach (Material material in selection.GetDescendants<Material>())
            AddModel(models, seen, material.EnumerateAncestors<Model>().FirstOrDefault());

        return models;
    }
}
