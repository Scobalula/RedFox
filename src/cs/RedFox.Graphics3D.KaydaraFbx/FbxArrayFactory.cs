namespace RedFox.Graphics3D.KaydaraFbx;

/// <summary>
/// Represents a factory that decodes a raw FBX array-property payload into a typed value array.
/// </summary>
/// <param name="bytes">The raw payload bytes for the array values.</param>
/// <param name="count">The decoded element count.</param>
/// <returns>The typed array instance.</returns>
public delegate object FbxArrayFactory(ReadOnlySpan<byte> bytes, int count);