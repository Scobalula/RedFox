using System.Numerics;

namespace RedFox.Graphics3D.Buffers.PackedVector
{
    /// <summary>
    /// Packs a 4-component ARGB color (8-8-8-8) normalized into a 32-bit integer.
    /// </summary>
    /// <remarks>
    /// Layout: [32] AAAAAAAA RRRRRRRR GGGGGGGG BBBBBBBB [0] (A8R8G8B8).
    /// </remarks>
    public struct Color : IPackedVector<Color>
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
        public Color(uint packedValue) => _packed = packedValue;

        /// <summary>
        /// Initializes a new instance from float components.
        /// </summary>
        /// <param name="r">The red component.</param>
        /// <param name="g">The green component.</param>
        /// <param name="b">The blue component.</param>
        /// <param name="a">The alpha component.</param>
        public Color(float r, float g, float b, float a) => Pack(new Vector4(r, g, b, a));

        /// <inheritdoc/>
        public void Pack(Vector4 value)
        {
            var r = (byte)Math.Clamp(MathF.Round(Math.Clamp(value.X, 0f, 1f) * 255f), 0, 255);
            var g = (byte)Math.Clamp(MathF.Round(Math.Clamp(value.Y, 0f, 1f) * 255f), 0, 255);
            var b = (byte)Math.Clamp(MathF.Round(Math.Clamp(value.Z, 0f, 1f) * 255f), 0, 255);
            var a = (byte)Math.Clamp(MathF.Round(Math.Clamp(value.W, 0f, 1f) * 255f), 0, 255);
            _packed = b | ((uint)g << 8) | ((uint)r << 16) | ((uint)a << 24);
        }

        /// <inheritdoc/>
        public readonly Vector4 Unpack()
        {
            var b = _packed & 0xFF;
            var g = (_packed >> 8) & 0xFF;
            var r = (_packed >> 16) & 0xFF;
            var a = (_packed >> 24) & 0xFF;
            return new Vector4(r / 255f, g / 255f, b / 255f, a / 255f);
        }
    }
}
