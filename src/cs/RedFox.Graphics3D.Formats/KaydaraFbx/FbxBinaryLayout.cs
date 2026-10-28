namespace RedFox.Graphics3D.Formats.KaydaraFbx;

/// <summary>
/// Describes the record layout of a binary FBX file.
/// </summary>
/// <param name="Is64Bit">Whether node records use 64-bit offsets, as in version 7500 and later.</param>
/// <param name="BigEndian">Whether values are stored big-endian.</param>
public readonly record struct FbxBinaryLayout(bool Is64Bit, bool BigEndian);
