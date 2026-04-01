using System.Numerics;

namespace RedFox.Graphics3D.Buffers.PackedVector;

/// <summary>
/// Packs a 3-component unsigned 5-6-5 integer vector into a 16-bit value.
/// </summary>
/// <remarks>
/// Layout: X:5 Y:6 Z:5.
/// </remarks>
public struct U565 : IPackedVector<U565>
{
    private ushort _packed;

    /// <inheritdoc/>
    public static int ComponentCount => 3;

    /// <summary>
    /// Gets or sets the raw packed value.
    /// </summary>
    public ushort PackedValue { readonly get => _packed; set => _packed = value; }

    /// <summary>
    /// Initializes a new instance from a raw packed value.
    /// </summary>
    /// <param name="packedValue">The raw packed value.</param>
    public U565(ushort packedValue) => _packed = packedValue;

    /// <summary>
    /// Initializes a new instance from float components.
    /// </summary>
    /// <param name="x">The X component.</param>
    /// <param name="y">The Y component.</param>
    /// <param name="z">The Z component.</param>
    public U565(float x, float y, float z) => Pack(new Vector4(x, y, z, 0f));

    /// <inheritdoc/>
    public void Pack(Vector4 value)
    {
        var x = (int)Math.Clamp(MathF.Round(value.X), 0, 31);
        var y = (int)Math.Clamp(MathF.Round(value.Y), 0, 63);
        var z = (int)Math.Clamp(MathF.Round(value.Z), 0, 31);
        _packed = (ushort)((z << 11) | (y << 5) | x);
    }

    /// <inheritdoc/>
    public readonly Vector4 Unpack()
    {
        var x = _packed & 0x1F;
        var y = (_packed >> 5) & 0x3F;
        var z = (_packed >> 11) & 0x1F;
        return new Vector4(x, y, z, 0f);
    }
}
