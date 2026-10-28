namespace RedFox.Graphics3D
{
    /// <summary>
    /// Identifies how a <see cref="Light"/> emits light.
    /// </summary>
    public enum LightType
    {
        /// <summary>
        /// Emits light in all directions from the node position.
        /// </summary>
        Point,

        /// <summary>
        /// Emits parallel light along the node's forward axis.
        /// </summary>
        Directional,

        /// <summary>
        /// Emits a cone of light along the node's forward axis.
        /// </summary>
        Spot,

        /// <summary>
        /// Emits light from a surface.
        /// </summary>
        Area,
    }
}
