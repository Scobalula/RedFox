using System.Numerics;

namespace RedFox.Graphics3D
{
    /// <summary>
    /// Represents the projection mode used by a <see cref="Camera"/>.
    /// </summary>
    public enum CameraProjection
    {
        /// <summary>
        /// Perspective projection — objects farther away appear smaller.
        /// </summary>
        Perspective,

        /// <summary>
        /// Orthographic projection — parallel projection with no foreshortening.
        /// </summary>
        Orthographic,
    }
}
