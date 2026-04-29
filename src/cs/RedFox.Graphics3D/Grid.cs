using System.Numerics;

namespace RedFox.Graphics3D
{
    /// <summary>
    /// Represents scene-owned ground grid settings.
    /// The grid is rendered by the active renderer and does not participate in the scene graph.
    /// </summary>
    public sealed class Grid
    {
        private const float MinimumSceneSpan = 0.001f;
        private const float TargetMinorCellCount = 24.0f;

        /// <summary>
        /// Gets or sets a value indicating whether the scene grid is rendered.
        /// </summary>
        public bool Enabled { get; set; } = false;

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
        public float LineWidth { get; set; } = 1.1f;

        /// <summary>
        /// Gets or sets the width multiplier applied to the outermost grid lines.
        /// A value of 1 leaves the border the same width as interior lines.
        /// </summary>
        public float EdgeLineWidthScale { get; set; } = 1.2f;

        /// <summary>
        /// Gets or sets the minimum number of pixels between cells before the shader switches to a coarser level of detail.
        /// </summary>
        public float MinimumPixelsBetweenCells { get; set; } = 2.5f;

        /// <summary>
        /// Gets or sets the color of minor grid lines.
        /// </summary>
        public Vector4 MinorColor { get; set; } = new Vector4(0.34f, 0.38f, 0.45f, 0.48f);

        /// <summary>
        /// Gets or sets the color of major grid lines.
        /// </summary>
        public Vector4 MajorColor { get; set; } = new Vector4(0.52f, 0.58f, 0.68f, 0.66f);

        /// <summary>
        /// Gets or sets the color of the X axis line.
        /// </summary>
        public Vector4 AxisXColor { get; set; } = new Vector4(0.9f, 0.28f, 0.25f, 0.82f);

        /// <summary>
        /// Gets or sets the color of the Z axis line.
        /// </summary>
        public Vector4 AxisZColor { get; set; } = new Vector4(0.25f, 0.45f, 0.9f, 0.82f);

        internal IRenderHandle? GraphicsHandle { get; set; }

        /// <summary>
        /// Initializes a new grid settings instance with default rendering values.
        /// </summary>
        public Grid()
        {
        }

        /// <summary>
        /// Updates grid spacing, extent, and line styling from scene bounds so preview navigation feels consistent across source-unit scales.
        /// </summary>
        /// <param name="bounds">The scene bounds after any renderer-side axis conversion has been applied.</param>
        public void ConfigureForBounds(SceneBounds bounds)
        {
            if (!bounds.IsValid)
            {
                return;
            }

            Vector3 size = bounds.Size;
            float horizontalSpan = MathF.Max(size.X, size.Z);
            float previewSpan = MathF.Max(horizontalSpan, bounds.Radius * 1.25f);
            float safeSpan = MathF.Max(previewSpan, MinimumSceneSpan);
            float spacing = CalculateNiceSpacing(safeSpan / TargetMinorCellCount);

            Spacing = spacing;
            MajorStep = 10;
            LineWidth = 1.1f;
            EdgeLineWidthScale = 1.2f;
            MinimumPixelsBetweenCells = 2.5f;
        }

        private static float CalculateNiceSpacing(float rawSpacing)
        {
            if (!float.IsFinite(rawSpacing) || rawSpacing <= 0.0f)
            {
                return MinimumSceneSpan;
            }

            float exponent = MathF.Floor(MathF.Log10(rawSpacing));
            float scale = MathF.Pow(10.0f, exponent);
            float normalized = rawSpacing / scale;
            float niceNormalized = normalized <= 1.0f
                ? 1.0f
                : normalized <= 2.0f
                    ? 2.0f
                    : normalized <= 5.0f
                        ? 5.0f
                        : 10.0f;

            return niceNormalized * scale;
        }
    }
}

