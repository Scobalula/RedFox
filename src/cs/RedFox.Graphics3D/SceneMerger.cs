using System;
using System.Linq;

namespace RedFox.Graphics3D;

/// <summary>
/// Commits scene nodes from a source (staging) hierarchy into a target hierarchy,
/// detecting and resolving duplicate nodes according to a <see cref="SceneMergeOptions"/>.
/// </summary>
public static class SceneMerger
{
    /// <summary>
    /// Commits each child of <paramref name="source"/> into <paramref name="targetParent"/>.
    /// </summary>
    /// <param name="targetParent">The node the source children are committed into.</param>
    /// <param name="source">The staging node whose children are committed.</param>
    /// <param name="options">The options controlling duplicate detection and resolution.</param>
    public static void MergeChildren(SceneNode targetParent, SceneNode source, SceneMergeOptions options)
    {
        ArgumentNullException.ThrowIfNull(targetParent);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);

        foreach (var child in source.EnumerateChildren().ToArray())
            MergeNode(targetParent, child, options, source);
    }

    /// <summary>
    /// Commits a single <paramref name="incoming"/> node into <paramref name="targetParent"/>,
    /// resolving any duplicate according to <paramref name="options"/>.
    /// </summary>
    /// <param name="targetParent">The node the incoming node is committed into.</param>
    /// <param name="incoming">The node to commit.</param>
    /// <param name="options">The options controlling duplicate detection and resolution.</param>
    /// <returns>The node that represents the committed result in the target hierarchy.</returns>
    public static SceneNode MergeNode(SceneNode targetParent, SceneNode incoming, SceneMergeOptions options)
    {
        ArgumentNullException.ThrowIfNull(targetParent);
        ArgumentNullException.ThrowIfNull(incoming);
        ArgumentNullException.ThrowIfNull(options);

        return MergeNode(targetParent, incoming, options, incoming.GetRoot());
    }

    private static SceneNode MergeNode(SceneNode targetParent, SceneNode incoming, SceneMergeOptions options, SceneNode stagingRoot)
    {
        var existing = incoming.FindDuplicateInScope(targetParent, options.DuplicateScope, incoming.Name, options.NameComparison, options.MatchType);
        if (existing is null)
        {
            incoming.MoveTo(targetParent, options.TransformMode, SceneNodeMatchScope.None, SceneMergeStrategy.Throw);
            return incoming;
        }

        switch (options.Strategy)
        {
            case SceneMergeStrategy.Throw:
                throw new SceneNodeDuplicateException($"A node with the name: {incoming.Name} already exists in: {targetParent.Name}");

            case SceneMergeStrategy.Rename:
                incoming.Name = incoming.MakeUniqueName(targetParent, SceneNodeMatchScope.Siblings, options.NameComparison);
                incoming.MoveTo(targetParent, options.TransformMode, SceneNodeMatchScope.None, SceneMergeStrategy.Throw);
                return incoming;

            case SceneMergeStrategy.Skip:
                SceneNode.RedirectReferences(incoming, existing, targetParent.GetRoot(), stagingRoot);
                incoming.Detach();
                return existing;

            case SceneMergeStrategy.Replace:
                SceneNode.RedirectReferences(existing, incoming, targetParent.GetRoot(), stagingRoot);
                existing.Detach();
                incoming.MoveTo(targetParent, options.TransformMode, SceneNodeMatchScope.None, SceneMergeStrategy.Throw);
                return incoming;

            case SceneMergeStrategy.Merge:
                SceneNode.ApplyMergeTransform(existing, incoming, options.TransformMode);
                foreach (var child in incoming.EnumerateChildren().ToArray())
                    MergeNode(existing, child, options, stagingRoot);
                SceneNode.RedirectReferences(incoming, existing, targetParent.GetRoot(), stagingRoot);
                incoming.Detach();
                return existing;

            default:
                throw new ArgumentOutOfRangeException(nameof(options), options.Strategy, "Unknown merge strategy.");
        }
    }

}
