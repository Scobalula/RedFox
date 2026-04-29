using RedFox.Graphics3D.Skeletal;

namespace RedFox.Graphics3D.Avalonia;

/// <summary>
/// Represents one plottable component from a skeletal animation curve.
/// </summary>
public sealed class SkeletonAnimationCurveComponent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SkeletonAnimationCurveComponent"/> class.
    /// </summary>
    /// <param name="animation">The source animation.</param>
    /// <param name="track">The source animation track.</param>
    /// <param name="curveName">The curve name.</param>
    /// <param name="curve">The source curve.</param>
    /// <param name="componentIndex">The zero-based component index.</param>
    /// <param name="componentName">The display component name.</param>
    public SkeletonAnimationCurveComponent(
        SkeletonAnimation animation,
        SkeletonAnimationTrack track,
        string curveName,
        AnimationCurve curve,
        int componentIndex,
        string componentName)
    {
        ArgumentNullException.ThrowIfNull(animation);
        ArgumentNullException.ThrowIfNull(track);
        ArgumentException.ThrowIfNullOrWhiteSpace(curveName);
        ArgumentNullException.ThrowIfNull(curve);
        ArgumentException.ThrowIfNullOrWhiteSpace(componentName);

        if (curve.Values is null || componentIndex < 0 || componentIndex >= curve.ComponentCount)
        {
            throw new ArgumentOutOfRangeException(nameof(componentIndex));
        }

        Animation = animation;
        Track = track;
        CurveName = curveName;
        Curve = curve;
        ComponentIndex = componentIndex;
        ComponentName = componentName;
        KeyFrameCount = Math.Min(curve.Keys?.ElementCount ?? 0, curve.Values.ElementCount);
    }

    /// <summary>
    /// Gets the source animation.
    /// </summary>
    public SkeletonAnimation Animation { get; }

    /// <summary>
    /// Gets the source animation track.
    /// </summary>
    public SkeletonAnimationTrack Track { get; }

    /// <summary>
    /// Gets the curve name.
    /// </summary>
    public string CurveName { get; }

    /// <summary>
    /// Gets the source curve.
    /// </summary>
    public AnimationCurve Curve { get; }

    /// <summary>
    /// Gets the zero-based component index.
    /// </summary>
    public int ComponentIndex { get; }

    /// <summary>
    /// Gets the component display name.
    /// </summary>
    public string ComponentName { get; }

    /// <summary>
    /// Gets the number of keyed values available for this component.
    /// </summary>
    public int KeyFrameCount { get; }

    /// <summary>
    /// Gets the display name for this component.
    /// </summary>
    public string DisplayName => $"{Track.Name} / {CurveName}.{ComponentName}";

    /// <summary>
    /// Gets the frame value at the supplied key index.
    /// </summary>
    /// <param name="keyIndex">The zero-based key index.</param>
    /// <returns>The key frame.</returns>
    public float GetFrame(int keyIndex)
    {
        ValidateKeyIndex(keyIndex);
        return Curve.GetKeyTime(keyIndex);
    }

    /// <summary>
    /// Gets the component value at the supplied key index.
    /// </summary>
    /// <param name="keyIndex">The zero-based key index.</param>
    /// <returns>The key value.</returns>
    public float GetValue(int keyIndex)
    {
        ValidateKeyIndex(keyIndex);
        return Curve.Values!.Get<float>(keyIndex, 0, ComponentIndex);
    }

    /// <summary>
    /// Creates a selected-key value for the supplied key index.
    /// </summary>
    /// <param name="keyIndex">The zero-based key index.</param>
    /// <returns>The selected key value.</returns>
    public SkeletonAnimationCurveKey CreateKey(int keyIndex)
    {
        return new SkeletonAnimationCurveKey(this, keyIndex, GetFrame(keyIndex), GetValue(keyIndex));
    }

    /// <inheritdoc/>
    public override string ToString() => DisplayName;

    private void ValidateKeyIndex(int keyIndex)
    {
        if (keyIndex < 0 || keyIndex >= KeyFrameCount)
        {
            throw new ArgumentOutOfRangeException(nameof(keyIndex));
        }
    }
}