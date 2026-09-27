using System.Numerics;
using RedFox.Graphics3D.Solvers;

namespace RedFox.Graphics3D;

/// <summary>
/// Represents an IK handle that drives the chain from <see cref="StartNode"/> down to <see cref="EndNode"/> toward a target.
/// </summary>
/// <param name="name">The name of the node.</param>
/// <param name="startNode">The first node of the chain.</param>
/// <param name="endNode">The last node of the chain, which reaches for the target.</param>
public sealed class IKHandleNode(string name, SceneNode startNode, SceneNode endNode) : SceneNode(name)
{
    /// <summary>
    /// Gets or sets the first node of the chain.
    /// </summary>
    public SceneNode StartNode { get; set; } = startNode;

    /// <summary>
    /// Gets or sets the last node of the chain, which reaches for the target.
    /// </summary>
    public SceneNode EndNode { get; set; } = endNode;

    /// <summary>
    /// Gets or sets the node the chain reaches for.
    /// </summary>
    public SceneNode? TargetNode { get; set; }

    /// <summary>
    /// Gets or sets the world space offset added to the target.
    /// </summary>
    public Vector3 TargetOffset { get; set; }

    /// <summary>
    /// Gets or sets the node the chain bends toward.
    /// </summary>
    public SceneNode? PoleVectorNode { get; set; }

    /// <summary>
    /// Gets or sets the node controlling the twist of the chain.
    /// </summary>
    public SceneNode? PoleNode { get; set; }

    /// <summary>
    /// Gets or sets whether the end node takes the rotation of the target.
    /// </summary>
    public bool UseTargetRotation { get; set; }

    /// <summary>
    /// Gets or sets the weight of the handle.
    /// </summary>
    public float Weight { get; set; } = 1.0f;

    /// <summary>
    /// Gets the nodes of the chain from <see cref="StartNode"/> to <see cref="EndNode"/>.
    /// </summary>
    /// <returns>The chain, or an empty array if <see cref="EndNode"/> is not a descendant of <see cref="StartNode"/>.</returns>
    public SceneNode[] GetChain()
    {
        var chain = new List<SceneNode> { EndNode };

        for (var node = EndNode.Parent; node is not null && chain[^1] != StartNode; node = node.Parent)
            chain.Add(node);

        chain.Reverse();
        return chain[0] == StartNode ? [.. chain] : [];
    }

    /// <summary>
    /// Creates the runtime solver for this handle: a two-bone solver for three node chains, otherwise a CCD solver.
    /// </summary>
    /// <returns>The runtime IK solver.</returns>
    public AnimationSamplerSolver CreateSolver()
    {
        var chain = GetChain();

        if (chain.Length == 3)
            return new TwoBoneIKSolver(Name, chain[0], chain[1], chain[2]) { CurrentWeight = Weight, TargetNode = TargetNode, TargetOffset = TargetOffset, PoleNode = PoleVectorNode, UseTargetRotation = UseTargetRotation };

        var solver = new CCDIKSolver(Name) { CurrentWeight = Weight, TargetNode = TargetNode, TargetOffset = TargetOffset, UseTargetRotation = UseTargetRotation };
        solver.Chain.AddRange(chain);
        return solver;
    }

    /// <inheritdoc/>
    public override void Swap(SceneNode oldNode, SceneNode newNode)
    {
        base.Swap(oldNode, newNode);

        if (ReferenceEquals(StartNode, oldNode))
            StartNode = newNode;
        if (ReferenceEquals(EndNode, oldNode))
            EndNode = newNode;
        if (ReferenceEquals(TargetNode, oldNode))
            TargetNode = newNode;
        if (ReferenceEquals(PoleVectorNode, oldNode))
            PoleVectorNode = newNode;
        if (ReferenceEquals(PoleNode, oldNode))
            PoleNode = newNode;
    }

    /// <inheritdoc/>
    protected override void RemapClonedReferences(IReadOnlyDictionary<SceneNode, SceneNode> clones)
    {
        if (clones.TryGetValue(StartNode, out SceneNode? startCopy))
            StartNode = startCopy;
        if (clones.TryGetValue(EndNode, out SceneNode? endCopy))
            EndNode = endCopy;
        if (TargetNode is not null && clones.TryGetValue(TargetNode, out SceneNode? targetCopy))
            TargetNode = targetCopy;
        if (PoleVectorNode is not null && clones.TryGetValue(PoleVectorNode, out SceneNode? poleVectorCopy))
            PoleVectorNode = poleVectorCopy;
        if (PoleNode is not null && clones.TryGetValue(PoleNode, out SceneNode? poleCopy))
            PoleNode = poleCopy;
    }
}
