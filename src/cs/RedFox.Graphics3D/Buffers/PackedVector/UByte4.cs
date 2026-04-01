using System.Numerics;

namespace RedFox.Graphics3D.Buffers.PackedVector;

/// <summary>
/// Packs a 4-component unsigned byte vector into a single <see cref="uint"/>.
/// </summary>
public struct UByte4 : IPackedVector<UByte4>
{
    private uint _packed;

    /// <inheritdoc/>
    public static int ComponentCount => 4;

    /// <summary>
    /// Gets or sets the raw packed value.
    /// </summary>
    public uint PackedValue { readonly get => _packed; set => _packed = value; }

    /// <summary>
    /// Initializes a new instance from a raw packed value.
    /// </summary>
    /// <param name="packedValue">The raw packed value.</param>
    public UByte4(uint packedValue) => _packed = packedValue;

    /// <summary>
    /// Initializes a new instance from float components.
    /// </summary>
    /// <param name="x">The X component.</param>
    /// <param name="y">The Y component.</param>
    /// <param name="z">The Z component.</param>
    /// <param name="w">The W component.</param>
    public UByte4(float x, float y, float z, float w) => Pack(new Vector4(x, y, z, w));

    /// <inheritdoc/>
    public void Pack(Vector4 value)
    {
        var x = (byte)Math.Clamp(MathF.Round(value.X), 0, 255);
        var y = (byte)Math.Clamp(MathF.Round(value.Y), 0, 255);
        var z = (byte)Math.Clamp(MathF.Round(value.Z), 0, 255);
        var w = (byte)Math.Clamp(MathF.Round(value.W), 0, 255);
        _packed = x | ((uint)y << 8) | ((uint)z << 16) | ((uint)w << 24);
    }

    /// <inheritdoc/>
    public readonly Vector4 Unpack()
    {
        var x = (byte)(_packed & 0xFF);
        var y = (byte)((_packed >> 8) & 0xFF);
        var z = (byte)((_packed >> 16) & 0xFF);
        var w = (byte)((_packed >> 24) & 0xFF);
        return new Vector4(x, y, z, w);
    }
}
