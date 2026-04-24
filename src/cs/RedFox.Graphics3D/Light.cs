using System.Numerics;
using RedFox.Graphics3D.Rendering.Backend;
using RedFox.Graphics3D.Rendering.Handles;
using RedFox.Graphics3D.Rendering.Materials;

namespace RedFox.Graphics3D
{
    /// <summary>
    /// Represents a point light in the scene graph.
    /// The light position is driven by the node transform so hosts can move it like any other scene node.
    /// </summary>
    public class Light : SceneNode
    {
        /// <summary>
        /// Gets or sets the world-space colour of the light.
        /// </summary>
        public Vector3 Color { get; set; } = Vector3.One;

        /// <summary>
        /// Gets or sets the luminous intensity of the light.
        /// </summary>
        public float Intensity { get; set; } = 1.0f;

        /// <summary>
        /// Gets or sets a value indicating whether the light contributes to rendering.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the local-space position convenience wrapper for the node transform.
        /// </summary>
        public Vector3 Position
        {
            get => GetBindWorldPosition();
            set => BindTransform.SetLocalPosition(value);
        }

        /// <summary>
        /// Initializes a new instance of <see cref="Light"/> with the default name.
        /// </summary>
        public Light() : this("Light")
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="Light"/> with the specified name.
        /// </summary>
        public Light(string name) : base(name)
        {
        }

        /// <inheritdoc/>
        public override IRenderHandle? CreateRenderHandle(IGraphicsDevice graphicsDevice, IMaterialTypeRegistry materialTypes)
        {
            return new LightRenderHandle(this);
        }
    }
}
