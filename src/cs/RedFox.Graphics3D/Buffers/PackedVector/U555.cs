using System.Numerics;

namespace RedFox.Graphics3D.Buffers.PackedVector;

/// <summary>
/// Packs a 4-component unsigned 5-5-5-1 integer vector into a 16-bit value.
/// </summary>
/// <remarks>
/// Layout: X:5 Y:5 Z:5 W:1.
/// </remarks>
public struct U555 : IPackedVector<U555>
{
    private ushort _packed;

    /// <inheritdoc/>
    public static int ComponentCount => 4;

    /// <summary>
    /// Gets or sets the raw packed value.
    /// </summary>
    public ushort PackedValue { readonly get => _packed; set => _packed = value; }

    /// <summary>
    /// Initializes a new instance from a raw packed value.
    /// </summary>
    /// <param name="packedValue">The raw packed value.</param>
    public U555(ushort packedValue) => _packed = packedValue;

    /// <summary>
    /// Initializes a new instance from float components.
    /// </summary>
    /// <param name="x">The X component.</param>
    /// <param name="y">The Y component.</param>
    /// <param name="z">The Z component.</param>
    /// <param name="w">The W component.</param>
    public U555(float x, float y, float z, float w) => Pack(new Vector4(x, y, z, w));

    /// <inheritdoc/>
    public void Pack(Vector4 value)
    {
        var x = (int)Math.Clamp(MathF.Round(value.X), 0, 31);
        var y = (int)Math.Clamp(MathF.Round(value.Y), 0, 31);
        var z = (int)Math.Clamp(MathF.Round(value.Z), 0, 31);
        var w = value.W > 0f ? 0x8000 : 0;
        _packed = (ushort)(w | (z << 10) | (y << 5) | x);
    }

    /// <inheritdoc/>
    public readonly Vector4 Unpack()
    {
        var x = _packed & 0x1F;
        var y = (_packed >> 5) & 0x1F;
        var z = (_packed >> 10) & 0x1F;
        var w = (_packed >> 15) & 0x1;
        return new Vector4(x, y, z, w);
    }
}
