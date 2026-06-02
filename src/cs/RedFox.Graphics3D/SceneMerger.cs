using System;
using System.Collections.Generic;
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
        var existing = FindDuplicate(targetParent, incoming, options);
        if (existing is null)
        {
            Attach(targetParent, incoming);
            return incoming;
        }

        switch (options.Strategy)
        {
            case SceneMergeStrategy.Throw:
                throw new SceneNodeDuplicateException($"A node with the name: {incoming.Name} already exists in: {targetParent.Name}");

            case SceneMergeStrategy.Rename:
                incoming.Name = MakeUniqueName(targetParent, incoming, options);
                Attach(targetParent, incoming);
                return incoming;

            case SceneMergeStrategy.Skip:
                RedirectReferences(incoming, existing, targetParent.GetRoot(), stagingRoot);
                incoming.Detach();
                return existing;

            case SceneMergeStrategy.Replace:
                RedirectReferences(existing, incoming, targetParent.GetRoot(), stagingRoot);
                existing.Detach();
                Attach(targetParent, incoming);
                return incoming;

            case SceneMergeStrategy.Merge:
                ApplyTransform(existing, incoming, options.TransformMode);
                foreach (var child in incoming.EnumerateChildren().ToArray())
                    MergeNode(existing, child, options, stagingRoot);
                RedirectReferences(incoming, existing, targetParent.GetRoot(), stagingRoot);
                incoming.Detach();
                return existing;

            default:
                throw new ArgumentOutOfRangeException(nameof(options), options.Strategy, "Unknown merge strategy.");
        }
    }

    private static void Attach(SceneNode targetParent, SceneNode incoming)
    {
        incoming.Detach();
        targetParent.AddNode(incoming);
    }

    private static SceneNode? FindDuplicate(SceneNode targetParent, SceneNode incoming, SceneMergeOptions options)
    {
        var scope = options.DuplicateScope;
        if (scope == SceneNodeMatchScope.None)
            return null;

        IEnumerable<SceneNode> candidates;
        if (scope.HasFlag(SceneNodeMatchScope.WholeScene))
        {
            candidates = targetParent.GetRoot().EnumerateHierarchy();
        }
        else
        {
            candidates = Enumerable.Empty<SceneNode>();
            if (scope.HasFlag(SceneNodeMatchScope.Siblings))
                candidates = candidates.Concat(targetParent.EnumerateChildren());
            if (scope.HasFlag(SceneNodeMatchScope.Descendants))
                candidates = candidates.Concat(targetParent.EnumerateDescendants());
        }

        foreach (var candidate in candidates)
        {
            if (ReferenceEquals(candidate, incoming))
                continue;
            if (!candidate.Name.Equals(incoming.Name, options.NameComparison))
                continue;
            if (options.MatchType && candidate.GetType() != incoming.GetType())
                continue;

            return candidate;
        }

        return null;
    }

    private static string MakeUniqueName(SceneNode targetParent, SceneNode incoming, SceneMergeOptions options)
    {
        var baseName = incoming.Name;
        var index = 1;
        string candidate;
        do
        {
            candidate = $"{baseName}_{index++}";
        }
        while (targetParent.EnumerateChildren().Any(x => x.Name.Equals(candidate, options.NameComparison)));

        return candidate;
    }

    private static void RedirectReferences(SceneNode oldNode, SceneNode newNode, SceneNode targetRoot, SceneNode stagingRoot)
    {
        foreach (var node in targetRoot.EnumerateHierarchy())
            node.Swap(oldNode, newNode);

        if (!ReferenceEquals(stagingRoot, targetRoot))
        {
            foreach (var node in stagingRoot.EnumerateHierarchy())
                node.Swap(oldNode, newNode);
        }
    }

    private static void ApplyTransform(SceneNode existing, SceneNode incoming, ReparentTransformMode mode)
    {
        switch (mode)
        {
            case ReparentTransformMode.PreserveExisting:
                break;

            case ReparentTransformMode.PreserveLocal:
                existing.BindTransform.LocalPosition = incoming.BindTransform.LocalPosition;
                existing.BindTransform.LocalRotation = incoming.BindTransform.LocalRotation;
                existing.BindTransform.Scale = incoming.BindTransform.Scale;
                existing.LiveTransform.LocalPosition = incoming.LiveTransform.LocalPosition;
                existing.LiveTransform.LocalRotation = incoming.LiveTransform.LocalRotation;
                existing.LiveTransform.Scale = incoming.LiveTransform.Scale;
                break;

            case ReparentTransformMode.PreserveWorld:
                existing.BindTransform.WorldPosition = incoming.BindTransform.WorldPosition;
                existing.BindTransform.WorldRotation = incoming.BindTransform.WorldRotation;
                existing.BindTransform.Scale = incoming.BindTransform.Scale;
                existing.LiveTransform.WorldPosition = incoming.LiveTransform.WorldPosition;
                existing.LiveTransform.WorldRotation = incoming.LiveTransform.WorldRotation;
                existing.LiveTransform.Scale = incoming.LiveTransform.Scale;
                break;
        }
    }
}
