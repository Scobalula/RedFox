
using System.Diagnostics;

namespace RedFox.Graphics3D
{
    /// <summary>
    /// Represents a named animation track that targets a single scene node.
    /// Contains translation, rotation, scale, and arbitrary custom curves,
    /// all backed by <see cref="AnimationCurve"/> (DataBuffer storage).
    /// <para>
    /// Extends <see cref="SceneNode"/> so tracks participate in the scene graph
    /// hierarchy alongside meshes, bones, and other nodes.
    /// </para>
    /// </summary>
    [DebuggerDisplay("AnimationTrack: {Name}, Curves = {CurveCount}")]
    public class AnimationTrack : SceneNode
    {
        /// <summary>
        /// Gets or sets the translation curve (3-component Vector3 values).
        /// <see langword="null"/> when no translation keyframes exist for this track.
        /// </summary>
        public AnimationCurve? TranslationCurve { get; set; }

        /// <summary>
        /// Gets or sets the rotation curve (4-component Quaternion values).
        /// <see langword="null"/> when no rotation keyframes exist for this track.
        /// </summary>
        public AnimationCurve? RotationCurve { get; set; }

        /// <summary>
        /// Gets or sets the scale curve (3-component Vector3 values).
        /// <see langword="null"/> when no scale keyframes exist for this track.
        /// </summary>
        public AnimationCurve? ScaleCurve { get; set; }

        /// <summary>
        /// Gets or sets additional named curves for custom animated properties
        /// (visibility, opacity, FOV, color, etc.). Lazily initialized.
        /// </summary>
        public Dictionary<string, AnimationCurve>? CustomCurves { get; set; }

        /// <summary>
        /// Gets the total number of assigned curves on this track (standard + custom).
        /// </summary>
        public int CurveCount
        {
            get
            {
                int count = 0;
                if (TranslationCurve is not null) count++;
                if (RotationCurve is not null) count++;
                if (ScaleCurve is not null) count++;
                if (CustomCurves is not null) count += CustomCurves.Count;
                return count;
            }
        }

        /// <summary>
        /// Initializes a new <see cref="AnimationTrack"/> with the specified target name.
        /// </summary>
        /// <param name="name">Name of the scene node this track targets (e.g., bone name).</param>
        public AnimationTrack(string name) : base(name) { }

        /// <summary>
        /// Initializes a new <see cref="AnimationTrack"/> with a generated name.
        /// </summary>
        public AnimationTrack() : base() { }

        // ------------------------------------------------------------------
        // Convenience methods for adding keyframes
        // ------------------------------------------------------------------

        /// <summary>
        /// Appends a translation keyframe. Lazily creates the translation curve
        /// in the specified transform space and type.
        /// </summary>
        /// <param name="time">Keyframe time.</param>
        /// <param name="value">Translation value.</param>
        /// <param name="space">Coordinate space.</param>
        /// <param name="type">Transform type.</param>
        public void AddTranslationFrame(float time, System.Numerics.Vector3 value, TransformSpace space, TransformType type)
        {
            TranslationCurve ??= AnimationCurve.CreateVector3(space, type);
            TranslationCurve.Add(time, value);
        }

        /// <inheritdoc cref="AddTranslationFrame(float, System.Numerics.Vector3, TransformSpace, TransformType)"/>
        public void AddTranslationFrame(float time, System.Numerics.Vector3 value) => AddTranslationFrame(time, value, TransformSpace.Local, TransformType.Absolute);

        /// <inheritdoc cref="AddTranslationFrame(float, System.Numerics.Vector3, TransformSpace, TransformType)"/>
        public void AddTranslationFrame(float time, System.Numerics.Vector3 value, TransformSpace space) => AddTranslationFrame(time, value, space, TransformType.Absolute);

        /// <inheritdoc cref="AddTranslationFrame(float, System.Numerics.Vector3, TransformSpace, TransformType)"/>
        public void AddTranslationFrame(float time, System.Numerics.Vector3 value, TransformType type) => AddTranslationFrame(time, value, TransformSpace.Local, type);

        /// <summary>
        /// Appends a rotation keyframe. Lazily creates the rotation curve
        /// in the specified transform space and type.
        /// </summary>
        /// <param name="time">Keyframe time.</param>
        /// <param name="value">Rotation value.</param>
        /// <param name="space">Coordinate space.</param>
        /// <param name="type">Transform type.</param>
        public void AddRotationFrame(float time, System.Numerics.Quaternion value, TransformSpace space, TransformType type)
        {
            RotationCurve ??= AnimationCurve.CreateQuaternion(space, type);
            RotationCurve.Add(time, value);
        }

        /// <inheritdoc cref="AddRotationFrame(float, System.Numerics.Quaternion, TransformSpace, TransformType)"/>
        public void AddRotationFrame(float time, System.Numerics.Quaternion value) => AddRotationFrame(time, value, TransformSpace.Local, TransformType.Absolute);

        /// <inheritdoc cref="AddRotationFrame(float, System.Numerics.Quaternion, TransformSpace, TransformType)"/>
        public void AddRotationFrame(float time, System.Numerics.Quaternion value, TransformSpace space) => AddRotationFrame(time, value, space, TransformType.Absolute);

        /// <inheritdoc cref="AddRotationFrame(float, System.Numerics.Quaternion, TransformSpace, TransformType)"/>
        public void AddRotationFrame(float time, System.Numerics.Quaternion value, TransformType type) => AddRotationFrame(time, value, TransformSpace.Local, type);

        /// <summary>
        /// Appends a scale keyframe. Lazily creates the scale curve
        /// in the specified transform space and type.
        /// </summary>
        /// <param name="time">Keyframe time.</param>
        /// <param name="value">Scale value.</param>
        /// <param name="space">Coordinate space.</param>
        /// <param name="type">Transform type.</param>
        public void AddScaleFrame(float time, System.Numerics.Vector3 value, TransformSpace space, TransformType type)
        {
            ScaleCurve ??= AnimationCurve.CreateVector3(space, type);
            ScaleCurve.Add(time, value);
        }

        /// <inheritdoc cref="AddScaleFrame(float, System.Numerics.Vector3, TransformSpace, TransformType)"/>
        public void AddScaleFrame(float time, System.Numerics.Vector3 value) => AddScaleFrame(time, value, TransformSpace.Local, TransformType.Absolute);

        /// <inheritdoc cref="AddScaleFrame(float, System.Numerics.Vector3, TransformSpace, TransformType)"/>
        public void AddScaleFrame(float time, System.Numerics.Vector3 value, TransformSpace space) => AddScaleFrame(time, value, space, TransformType.Absolute);

        /// <inheritdoc cref="AddScaleFrame(float, System.Numerics.Vector3, TransformSpace, TransformType)"/>
        public void AddScaleFrame(float time, System.Numerics.Vector3 value, TransformType type) => AddScaleFrame(time, value, TransformSpace.Local, type);

        /// <summary>
        /// Gets or creates a custom named curve with the specified component count.
        /// </summary>
        /// <param name="name">The property name (e.g. "Visibility", "FOV").</param>
        /// <param name="componentCount">Number of components per value (1, 3, or 4).</param>
        /// <returns>The existing or newly created curve.</returns>
        public AnimationCurve GetOrCreateCustomCurve(string name, int componentCount)
        {
            CustomCurves ??= [];

            if (CustomCurves.TryGetValue(name, out var existing))
                return existing;

            var curve = componentCount switch
            {
                1 => AnimationCurve.CreateScalar(),
                3 => AnimationCurve.CreateVector3(),
                4 => AnimationCurve.CreateQuaternion(),
                _ => AnimationCurve.CreateScalar()
            };

            CustomCurves[name] = curve;
            return curve;
        }

        /// <summary>
        /// Gets the minimum and maximum keyframe times across all curves on this track.
        /// </summary>
        /// <returns>
        /// A tuple of (minTime, maxTime). Returns <c>(float.MaxValue, float.MinValue)</c>
        /// if no keyframes exist.
        /// </returns>
        public (float Min, float Max) GetTimeRange()
        {
            var min = float.MaxValue;
            var max = float.MinValue;

            UpdateTimeRange(TranslationCurve, ref min, ref max);
            UpdateTimeRange(RotationCurve, ref min, ref max);
            UpdateTimeRange(ScaleCurve, ref min, ref max);

            if (CustomCurves is not null)
            {
                foreach (var curve in CustomCurves.Values)
                    UpdateTimeRange(curve, ref min, ref max);
            }

            return (min, max);
        }

        /// <summary>
        /// Returns <see langword="true"/> if this track has any keyframe data at all.
        /// </summary>
        public bool HasKeyFrames =>
            (TranslationCurve is not null && TranslationCurve.KeyFrameCount > 0) ||
            (RotationCurve is not null && RotationCurve.KeyFrameCount > 0) ||
            (ScaleCurve is not null && ScaleCurve.KeyFrameCount > 0);

        private static void UpdateTimeRange(AnimationCurve? curve, ref float min, ref float max)
        {
            if (curve is null || curve.KeyFrameCount == 0) return;
            min = MathF.Min(min, curve.StartTime);
            max = MathF.Max(max, curve.EndTime);
        }
    }
}
