using System.Numerics;

namespace RedFox.Graphics2D.BC;

/// <summary>
/// Delegate that decodes a single compressed block into 16 <see cref="Vector4"/> pixels
/// arranged in 4×4 row-major order.
/// </summary>
/// <param name="block">The raw compressed block data.</param>
/// <param name="pixels">The destination span receiving 16 decoded RGBA pixels.</param>
public delegate void BlockDecoder(ReadOnlySpan<byte> block, Span<Vector4> pixels);
