using System.Numerics;

namespace RedFox.Graphics3D
{
    /// <summary>
    /// Represents a camera in the scene graph. Provides view and projection matrices
    /// that can be fed directly to a <see cref="Rendering.SceneRenderer"/>.
    /// </summary>
    /// <remarks>
    /// The camera uses a right-handed coordinate system consistent with
    /// <see cref="Matrix4x4.CreateLookAt"/> and <see cref="Matrix4x4.CreatePerspectiveFieldOfView"/>.
    /// </remarks>
    public class Camera : SceneNode
    {
        private Vector3 _position = new(0, 1, 5);
        private Vector3 _target   = Vector3.Zero;
        private Vector3 _up       = Vector3.UnitY;

        /// <summary>
        /// Gets or sets the camera position in world space.
        /// </summary>
        public Vector3 Position
        {
            get => _position;
            set => _position = value;
        }

        /// <summary>
        /// Gets or sets the point in world space the camera looks at.
        /// </summary>
        public Vector3 Target
        {
            get => _target;
            set => _target = value;
        }

        /// <summary>
        /// Gets or sets the world-space up direction. Defaults to <see cref="Vector3.UnitY"/>.
        /// </summary>
        public Vector3 Up
        {
            get => _up;
            set => _up = value;
        }

        /// <summary>
        /// Gets or sets the vertical field of view in degrees. Only used for
        /// <see cref="CameraProjection.Perspective"/>.
        /// </summary>
        public float FieldOfView { get; set; } = 60f;

        /// <summary>
        /// Gets or sets the near clip plane distance.
        /// </summary>
        public float NearPlane { get; set; } = 0.01f;

        /// <summary>
        /// Gets or sets the far clip plane distance.
        /// </summary>
        public float FarPlane { get; set; } = 1000f;

        /// <summary>
        /// Gets or sets the viewport aspect ratio (width / height).
        /// </summary>
        public float AspectRatio { get; set; } = 16f / 9f;

        /// <summary>
        /// Gets or sets the orthographic view width. Only used for
        /// <see cref="CameraProjection.Orthographic"/>.
        /// </summary>
        public float OrthographicSize { get; set; } = 10f;

        /// <summary>
        /// Gets or sets the projection mode.
        /// </summary>
        public CameraProjection Projection { get; set; } = CameraProjection.Perspective;

        /// <summary>
        /// Initializes a new instance of <see cref="Camera"/> with the default name.
        /// </summary>
        public Camera() : base("Camera") { }

        /// <summary>
        /// Initializes a new instance of <see cref="Camera"/> with the specified name.
        /// </summary>
        /// <param name="name">The camera name.</param>
        public Camera(string name) : base(name) { }

        /// <summary>
        /// Computes the view matrix from the current position, target, and up direction.
        /// </summary>
        public Matrix4x4 GetViewMatrix() =>
            Matrix4x4.CreateLookAt(_position, _target, _up);

        /// <summary>
        /// Computes the projection matrix from the current camera parameters.
        /// </summary>
        public Matrix4x4 GetProjectionMatrix() => Projection switch
        {
            CameraProjection.Perspective =>
                Matrix4x4.CreatePerspectiveFieldOfView(
                    float.DegreesToRadians(FieldOfView),
                    AspectRatio,
                    NearPlane,
                    FarPlane),
            CameraProjection.Orthographic =>
                Matrix4x4.CreateOrthographic(
                    OrthographicSize * AspectRatio,
                    OrthographicSize,
                    NearPlane,
                    FarPlane),
            _ => Matrix4x4.Identity,
        };

        /// <summary>
        /// Returns the forward direction the camera is facing (normalised).
        /// </summary>
        public Vector3 GetForward() => Vector3.Normalize(_target - _position);

        /// <summary>
        /// Returns the right direction relative to the camera's orientation.
        /// </summary>
        public Vector3 GetRight() => Vector3.Normalize(Vector3.Cross(GetForward(), _up));
    }
}
