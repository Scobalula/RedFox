namespace RedFox.Graphics3D.Formats.KaydaraFbx;

/// <summary>
/// Represents an FBX object-to-object or object-to-property connection entry.
/// </summary>
/// <param name="ConnectionType">The FBX connection type token such as <c>OO</c> or <c>OP</c>.</param>
/// <param name="ChildId">The connected child object identifier.</param>
/// <param name="ParentId">The connected parent object identifier.</param>
/// <param name="PropertyName">The parent property name carried by <c>OP</c> connections, otherwise empty.</param>
public readonly record struct FbxConnection(string ConnectionType, long ChildId, long ParentId, string PropertyName);
