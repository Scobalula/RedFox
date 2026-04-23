using System.Numerics;

namespace RedFox.Graphics3D
{
    /// <summary>
    /// Represents a finite ground grid in the scene graph.
    /// The grid is a flat plane on the XZ world axes with configurable extent, spacing, colors, and visual effects.
    /// Each renderer backend (OpenGL, DirectX, Vulkan) maintains its own renderer-specific handle
    /// stored in the <see cref="SceneNode.GraphicsHandle"/> property.
    /// </summary>
    public sealed class Grid : SceneNode
    {
        /// <summary>
        /// Gets or sets the half-extent of the grid from the center to each edge.
        /// </summary>
        public float Size { get; set; } = 128.0f;

        /// <summary>
        /// Gets or sets the spacing between grid lines.
        /// </summary>
        public float Spacing { get; set; } = 2.0f;

        /// <summary>
        /// Gets or sets the major grid step (every N cells is a major line).
        /// </summary>
        public int MajorStep { get; set; } = 10;

        /// <summary>
        /// Gets or sets the pixel width of grid lines.
        /// </summary>
        public float LineWidth { get; set; } = 1.35f;

        /// <summary>
        /// Gets or sets the width multiplier applied to the outermost grid lines.
        /// A value of 1 leaves the border the same width as interior lines.
        /// </summary>
        public float EdgeLineWidthScale { get; set; } = 1.0f;

        /// <summary>
        /// Gets or sets the color of minor grid lines.
        /// </summary>
        public Vector4 MinorColor { get; set; } = new Vector4(0.26f, 0.29f, 0.34f, 0.55f);

        /// <summary>
        /// Gets or sets the color of major grid lines.
        /// </summary>
        public Vector4 MajorColor { get; set; } = new Vector4(0.38f, 0.42f, 0.5f, 0.7f);

        /// <summary>
        /// Gets or sets the color of the X axis line.
        /// </summary>
        public Vector4 AxisXColor { get; set; } = new Vector4(0.85f, 0.3f, 0.3f, 0.9f);

        /// <summary>
        /// Gets or sets the color of the Z axis line.
        /// </summary>
        public Vector4 AxisZColor { get; set; } = new Vector4(0.3f, 0.45f, 0.85f, 0.9f);

        /// <summary>
        /// Gets or sets whether distance-based fade is enabled.
        /// </summary>
        public bool FadeEnabled { get; set; } = false;

        /// <summary>
        /// Gets or sets the distance at which grid fade begins (0 = auto).
        /// </summary>
        public float FadeStartDistance { get; set; } = 0.0f;

        /// <summary>
        /// Gets or sets the distance at which grid fade ends (0 = auto).
        /// </summary>
        public float FadeEndDistance { get; set; } = 0.0f;

        /// <summary>
        /// Initializes a new grid instance with default settings.
        /// </summary>
        public Grid()
        {
            Name = "Grid";
        }
    }
}

