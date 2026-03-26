namespace RedFox.Graphics3D.MayaAscii;

/// <summary>
/// Represents a deferred <c>connectAttr</c> command that wires two Maya dependency graph
/// plugs together. Connections are accumulated during scene traversal and flushed at the
/// end of the file after all <c>createNode</c> commands have been emitted.
/// </summary>
/// <param name="Source">The source plug path (e.g., "joint1.worldMatrix[0]").</param>
/// <param name="Destination">The destination plug path (e.g., "skinCluster1.matrix[0]").</param>
/// <param name="IsNextAvailable">
/// When <see langword="true"/>, the <c>-na</c> (next available) flag is emitted,
/// causing Maya to automatically pick the next free array index on the destination plug.
/// </param>
public readonly record struct MayaConnection(string Source, string Destination, bool IsNextAvailable);
