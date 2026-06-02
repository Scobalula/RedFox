using System;

namespace RedFox.Graphics3D;

/// <summary>
/// Controls how nodes are committed into a scene, including duplicate detection and resolution.
/// </summary>
public sealed class SceneMergeOptions
{
    /// <summary>
    /// Gets a strict configuration that throws on duplicates found among siblings.
    /// This mirrors the default behavior of <see cref="SceneNode.AddNode{T}(T)"/>.
    /// </summary>
    public static SceneMergeOptions Strict => new();

    /// <summary>
    /// Gets the scope used to search for duplicate nodes. Defaults to <see cref="SceneNodeMatchScope.Siblings"/>.
    /// </summary>
    public SceneNodeMatchScope DuplicateScope { get; set; } = SceneNodeMatchScope.Siblings;

    /// <summary>
    /// Gets the strategy used to resolve a duplicate node. Defaults to <see cref="SceneMergeStrategy.Throw"/>.
    /// </summary>
    public SceneMergeStrategy Strategy { get; set; } = SceneMergeStrategy.Throw;

    /// <summary>
    /// Gets how transforms are reconciled when an incoming node is merged into an existing node.
    /// Defaults to <see cref="ReparentTransformMode.PreserveExisting"/>.
    /// </summary>
    public ReparentTransformMode TransformMode { get; set; } = ReparentTransformMode.PreserveExisting;

    /// <summary>
    /// Gets the comparison used to match node names. Defaults to <see cref="StringComparison.CurrentCultureIgnoreCase"/>.
    /// </summary>
    public StringComparison NameComparison { get; set; } = StringComparison.CurrentCultureIgnoreCase;

    /// <summary>
    /// Gets a value indicating whether a duplicate must be of the same runtime type as the incoming node.
    /// Defaults to <see langword="true"/>.
    /// </summary>
    public bool MatchType { get; set; } = true;
}
