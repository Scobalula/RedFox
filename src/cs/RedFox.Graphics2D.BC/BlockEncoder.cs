using System.Numerics;

namespace RedFox.Graphics2D.BC
{
    /// <summary>
    /// Delegate that encodes 16 <see cref="Vector4"/> pixels (4×4 row-major order)
    /// into a single compressed block.
    /// </summary>
    /// <param name="pixels">The 16 source RGBA pixels.</param>
    /// <param name="block">The destination span receiving the compressed block data.</param>
    internal delegate void BlockEncoder(ReadOnlySpan<Vector4> pixels, Span<byte> block);
}
