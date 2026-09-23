using System;
using RedFox.Graphics3D;

namespace RedFox.Graphics3D.IO;

/// <summary>
/// Provides a sandboxed staging scene that a translator reads into, then commits the
/// staged nodes into a target scene using a <see cref="SceneMergeOptions"/>. This allows
/// duplicate detection and resolution to be applied when reading into a scene that may
/// already contain nodes.
/// </summary>
public sealed class SceneReadContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SceneReadContext"/> class.
    /// </summary>
    /// <param name="target">The scene the staged nodes are committed into.</param>
    /// <param name="merge">The options controlling duplicate detection and resolution.</param>
    public SceneReadContext(Scene target, SceneMergeOptions merge)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(merge);

        Target = target;
        Merge = merge;
        Staging = new Scene(target.Name);
        Staging.ImageTranslators.RegisterRange(target.ImageTranslators.Translators);
    }

    /// <summary>
    /// Gets the scene the staged nodes are committed into.
    /// </summary>
    public Scene Target { get; }

    /// <summary>
    /// Gets the staging scene translators read into before committing.
    /// </summary>
    public Scene Staging { get; }

    /// <summary>
    /// Gets the options controlling duplicate detection and resolution.
    /// </summary>
    public SceneMergeOptions Merge { get; }

    /// <summary>
    /// Commits the staged nodes into the target scene. When the target scene is empty,
    /// scene-level settings read from the staging scene are adopted by the target.
    /// </summary>
    public void Commit()
    {
        bool targetIsEmpty = Target.RootNode.Children is null or { Count: 0 };
        if (targetIsEmpty)
        {
            Target.Name = Staging.Name;
            Target.UpAxis = Staging.UpAxis;
            Target.FaceWinding = Staging.FaceWinding;
        }

        SceneMerger.MergeChildren(Target.RootNode, Staging.RootNode, Merge);

        if (Staging.AnimationPlayers.Count > 0)
        {
            Target.AnimationPlayers.AddRange(Staging.AnimationPlayers);
            Staging.AnimationPlayers.Clear();
        }
    }
}
