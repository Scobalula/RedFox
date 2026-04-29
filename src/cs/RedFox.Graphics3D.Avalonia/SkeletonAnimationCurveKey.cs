namespace RedFox.Graphics3D.Avalonia;

/// <summary>
/// Represents a selected keyed value from a skeletal animation curve component.
/// </summary>
public sealed class SkeletonAnimationCurveKey
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SkeletonAnimationCurveKey"/> class.
    /// </summary>
    /// <param name="component">The selected component.</param>
    /// <param name="keyIndex">The zero-based key index.</param>
    /// <param name="frame">The key frame.</param>
    /// <param name="value">The keyed component value.</param>
    public SkeletonAnimationCurveKey(SkeletonAnimationCurveComponent component, int keyIndex, float frame, float value)
    {
        Component = component ?? throw new ArgumentNullException(nameof(component));
        KeyIndex = keyIndex;
        Frame = frame;
        Value = value;
    }

    /// <summary>
    /// Gets the component that owns this key.
    /// </summary>
    public SkeletonAnimationCurveComponent Component { get; }

    /// <summary>
    /// Gets the zero-based key index.
    /// </summary>
    public int KeyIndex { get; }

    /// <summary>
    /// Gets the key frame.
    /// </summary>
    public float Frame { get; }

    /// <summary>
    /// Gets the keyed component value.
    /// </summary>
    public float Value { get; }

    /// <inheritdoc/>
    public override string ToString() => $"{Component.DisplayName} [{KeyIndex}] Frame {Frame:0.###} Value {Value:0.###}";
}