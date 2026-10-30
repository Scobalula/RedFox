using System.Numerics;

namespace RedFox.Graphics3D.Formats.KaydaraFbx;

/// <summary>
/// Represents an animated FBX property with up to three component curves and per-component defaults.
/// </summary>
/// <param name="defaultValue">The value used for components without a curve.</param>
public sealed class FbxAnimationChannel(Vector3 defaultValue)
{
    /// <summary>
    /// Gets the component curves, indexed by component.
    /// </summary>
    public FbxAnimationCurve?[] Curves { get; } = new FbxAnimationCurve?[3];

    /// <summary>
    /// Gets or sets the value used for components without a curve.
    /// </summary>
    public Vector3 Default { get; set; } = defaultValue;

    /// <summary>
    /// Evaluates the channel at a time.
    /// </summary>
    /// <param name="time">The time in FBX ticks.</param>
    /// <returns>The channel value.</returns>
    public Vector3 Evaluate(long time)
    {
        return new Vector3(Curves[0]?.Evaluate(time) ?? Default.X, Curves[1]?.Evaluate(time) ?? Default.Y, Curves[2]?.Evaluate(time) ?? Default.Z);
    }
}
