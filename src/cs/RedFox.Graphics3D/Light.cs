using System.Numerics;
using RedFox.Graphics3D.IO.SceneFile;

namespace RedFox.Graphics3D
{
    /// <summary>
    /// Represents a light in the scene graph.
    /// The light position is driven by the node transform so hosts can move it like any other scene node.
    /// </summary>
    /// <param name="name">The light name.</param>
    public class Light(string name) : SceneNode(name)
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
        /// Gets or sets how the light emits light.
        /// </summary>
        public LightType Type { get; set; }

        /// <summary>
        /// Gets or sets the angle in degrees of the fully lit inner cone of a spot light.
        /// </summary>
        public float InnerConeAngle { get; set; }

        /// <summary>
        /// Gets or sets the angle in degrees of the outer cone of a spot light.
        /// </summary>
        public float OuterConeAngle { get; set; } = 45.0f;

        /// <summary>
        /// Gets or sets the world-space position of the light.
        /// </summary>
        public Vector3 Position
        {
            get => GetBindWorldPosition();
            set => BindTransform.SetWorldPosition(value);
        }

        /// <summary>
        /// Initializes a new instance of <see cref="Light"/> with the default name.
        /// </summary>
        public Light() : this("Light")
        {
        }

        /// <inheritdoc/>
        public override void Write(BinaryWriter writer, IReadOnlyDictionary<SceneNode, int> nodeIndices)
        {
            base.Write(writer, nodeIndices);
            writer.WriteValue(Color);
            writer.Write(Intensity);
            writer.Write(Enabled);
            writer.Write((int)Type);
            writer.Write(InnerConeAngle);
            writer.Write(OuterConeAngle);
        }

        /// <inheritdoc/>
        public override void Read(BinaryReader reader, IReadOnlyList<SceneNode> nodes)
        {
            base.Read(reader, nodes);
            Color			= reader.ReadValue<Vector3>();
            Intensity		= reader.ReadSingle();
            Enabled			= reader.ReadBoolean();
            Type			= (LightType)reader.ReadInt32();
            InnerConeAngle	= reader.ReadSingle();
            OuterConeAngle	= reader.ReadSingle();
        }

    }
}
