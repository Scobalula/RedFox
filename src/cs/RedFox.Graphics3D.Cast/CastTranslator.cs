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
    public override void Read(Scene scene, Stream stream, string name, SceneTranslatorOptions options, CancellationToken? token)
    {
        var cast = CastReader.Load(stream);
        var root = cast.RootNodes[0];

        foreach (var modelNode in root.EnumerateChildrenOfType<ModelNode>())
        {
            CastModelTranslator.Read(scene, modelNode, name);
        }

        foreach (var animationNode in root.EnumerateChildrenOfType<AnimationNode>())
        {
            CastAnimationTranslator.Read(scene, animationNode, name);
        }
    }

    /// <inheritdoc/>
    public override void Write(Scene scene, Stream stream, string name, SceneTranslatorOptions options, CancellationToken? token)
    {
        var root = new CastNode(CastNodeIdentifier.Root);

        var metaDataNode = root.AddNode<MetadataNode>();
        metaDataNode.AddString("up", "z");
        metaDataNode.AddString("s", Assembly.GetEntryAssembly()?.GetName().Name ?? string.Empty);

        foreach (var model in scene.RootNode.EnumerateDescendants<Model>())
        {
            CastModelTranslator.Write(root, model, scene);
        }

        foreach (var animation in scene.RootNode.EnumerateDescendants<SkeletonAnimation>())
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
}
