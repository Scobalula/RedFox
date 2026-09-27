using System.Numerics;

namespace RedFox.Graphics3D
{
    /// <summary>
    /// Represents an axis-aligned world-space bounds volume for a scene.
    /// </summary>
    /// <param name="min">The minimum world-space corner.</param>
    /// <param name="max">The maximum world-space corner.</param>
    public readonly struct SceneBounds(Vector3 min, Vector3 max)
    {
        /// <summary>
        /// Gets the minimum world-space corner.
        /// </summary>
        public Vector3 Min { get; } = min;

        /// <summary>
        /// Gets the maximum world-space corner.
        /// </summary>
        public Vector3 Max { get; } = max;

        /// <summary>
        /// Gets a value indicating whether this bounds volume is valid.
        /// </summary>
        public bool IsValid { get; } = true;

        /// <summary>
        /// Gets the world-space center.
        /// </summary>
        public Vector3 Center => IsValid ? (Min + Max) * 0.5f : Vector3.Zero;

        /// <summary>
        /// Gets the world-space size (max - min).
        /// </summary>
        public Vector3 Size => IsValid ? Max - Min : Vector3.Zero;

        /// <summary>
        /// Gets the world-space half extents.
        /// </summary>
        public Vector3 Extents => Size * 0.5f;

        /// <summary>
        /// Gets the diagonal length of the bounds volume.
        /// </summary>
        public float DiagonalLength => Size.Length();

        /// <summary>
        /// Gets the radius of the smallest sphere centered at <see cref="Center"/> that encloses the bounds.
        /// </summary>
        public float Radius => Extents.Length();

        /// <summary>
        /// Gets an invalid bounds value.
        /// </summary>
        public static SceneBounds Invalid => default;

        /// <summary>
        /// Returns bounds expanded to contain the specified point.
        /// </summary>
        /// <param name="point">The point to include.</param>
        /// <returns>The expanded bounds.</returns>
        public SceneBounds Include(Vector3 point) => IsValid ? new SceneBounds(Vector3.Min(Min, point), Vector3.Max(Max, point)) : new SceneBounds(point, point);

        /// <summary>
        /// Returns bounds expanded to contain the specified bounds.
        /// </summary>
        /// <param name="bounds">The bounds to include.</param>
        /// <returns>The expanded bounds.</returns>
        public SceneBounds Include(SceneBounds bounds) => bounds.IsValid ? Include(bounds.Min).Include(bounds.Max) : this;

        /// <summary>
        /// Returns bounds grown by the specified amount along every axis.
        /// </summary>
        /// <param name="amount">The distance to grow each face by.</param>
        /// <returns>The expanded bounds, or these bounds when invalid.</returns>
        public SceneBounds Expand(float amount) => IsValid ? new SceneBounds(Min - new Vector3(amount), Max + new Vector3(amount)) : this;

        /// <summary>
        /// Returns these bounds transformed by the specified matrix.
        /// </summary>
        /// <param name="transform">The transform to apply.</param>
        /// <returns>The transformed bounds.</returns>
        public SceneBounds Transform(Matrix4x4 transform)
        {
            if (!IsValid)
                return Invalid;

            SceneBounds result = Invalid;

            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = new((corner & 1) == 0 ? Min.X : Max.X, (corner & 2) == 0 ? Min.Y : Max.Y, (corner & 4) == 0 ? Min.Z : Max.Z);
                result = result.Include(Vector3.Transform(point, transform));
            }

            return result;
        }
    }
}
