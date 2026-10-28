namespace RedFox.Graphics3D.Formats.KaydaraFbx;

/// <summary>
/// Identifies the encoded type of an <see cref="FbxProperty"/> using the FBX binary type codes.
/// </summary>
public enum FbxPropertyType : byte
{
    /// <summary>
    /// A signed 16-bit integer.
    /// </summary>
    Int16 = (byte)'Y',

    /// <summary>
    /// An unsigned 8-bit integer.
    /// </summary>
    Byte = (byte)'Z',

    /// <summary>
    /// A boolean stored as a single byte.
    /// </summary>
    Boolean = (byte)'C',

    /// <summary>
    /// A signed 32-bit integer.
    /// </summary>
    Int32 = (byte)'I',

    /// <summary>
    /// A 32-bit floating point value.
    /// </summary>
    Single = (byte)'F',

    /// <summary>
    /// A 64-bit floating point value.
    /// </summary>
    Double = (byte)'D',

    /// <summary>
    /// A signed 64-bit integer.
    /// </summary>
    Int64 = (byte)'L',

    /// <summary>
    /// A string.
    /// </summary>
    String = (byte)'S',

    /// <summary>
    /// Raw binary data.
    /// </summary>
    Raw = (byte)'R',

    /// <summary>
    /// An array of 32-bit floating point values.
    /// </summary>
    SingleArray = (byte)'f',

    /// <summary>
    /// An array of 64-bit floating point values.
    /// </summary>
    DoubleArray = (byte)'d',

    /// <summary>
    /// An array of signed 32-bit integers.
    /// </summary>
    Int32Array = (byte)'i',

    /// <summary>
    /// An array of signed 64-bit integers.
    /// </summary>
    Int64Array = (byte)'l',

    /// <summary>
    /// An array of booleans.
    /// </summary>
    BooleanArray = (byte)'b',

    /// <summary>
    /// An array of bytes.
    /// </summary>
    ByteArray = (byte)'c',
}
