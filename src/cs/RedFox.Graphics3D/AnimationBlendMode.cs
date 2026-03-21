namespace RedFox.Graphics3D;

/// <summary>
/// Specifies how an animation layer's sampled values are combined with
/// layers below it in the animation stack.
/// </summary>
public enum AnimationBlendMode
{
    /// <summary>
    /// The layer's values fully replace the values from lower layers,
    /// modulated by the layer weight. This is the standard blend mode
    /// for base animation layers.
    /// <para>
    /// <c>result = lerp(lower, upper, weight)</c>
    /// </para>
    /// </summary>
    Override,

    /// <summary>
    /// The layer's values are added on top of the values from lower layers,
    /// scaled by the layer weight. Useful for layering partial animations
    /// (e.g. breathing, recoil) onto a base pose.
    /// <para>
    /// Translation: <c>result = lower + upper * weight</c><br/>
    /// Rotation: <c>result = lower * slerp(identity, upper, weight)</c>
    /// </para>
    /// </summary>
    Additive,
}
