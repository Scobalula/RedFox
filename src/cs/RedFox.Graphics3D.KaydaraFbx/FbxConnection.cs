namespace RedFox.Graphics3D.KaydaraFbx;

/// <summary>
/// Represents an FBX object-to-object or object-to-property connection entry.
/// </summary>
/// <param name="ConnectionType">The FBX connection type token such as <c>OO</c> or <c>OP</c>.</param>
/// <param name="ChildId">The connected child object identifier.</param>
/// <param name="ParentId">The connected parent object identifier.</param>
public readonly record struct FbxConnection(string ConnectionType, long ChildId, long ParentId);
