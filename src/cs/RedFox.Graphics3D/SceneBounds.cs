using System.Numerics;

namespace RedFox.Graphics3D
{
    /// <summary>
    /// Represents an axis-aligned world-space bounds volume for a scene.
    /// </summary>
    public readonly struct SceneBounds
    {
        /// <summary>
        /// Gets the minimum world-space corner.
        /// </summary>
        public Vector3 Min { get; }

        /// <summary>
        /// Gets the maximum world-space corner.
        /// </summary>
        public Vector3 Max { get; }

        /// <summary>
        /// Gets a value indicating whether this bounds volume is valid.
        /// </summary>
        public bool IsValid { get; }

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
        /// Initializes a new instance of <see cref="SceneBounds"/>.
        /// </summary>
        /// <param name="min">The minimum world-space corner.</param>
        /// <param name="max">The maximum world-space corner.</param>
        public SceneBounds(Vector3 min, Vector3 max)
        {
            Min = min;
            Max = max;
            IsValid = true;
        }

        /// <summary>
        /// Gets an invalid bounds value.
        /// </summary>
        public static SceneBounds Invalid => default;
    }
}
