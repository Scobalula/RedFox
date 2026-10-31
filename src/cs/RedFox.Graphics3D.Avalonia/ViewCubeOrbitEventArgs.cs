namespace RedFox.Graphics3D.Avalonia;

/// <summary>
/// Carries an orientation or a change of orientation requested through a view cube.
/// </summary>
/// <param name="orbit">The orientation.</param>
public sealed class ViewCubeOrbitEventArgs(ViewCubeOrbit orbit) : EventArgs
{
    /// <summary>
    /// Gets the orientation.
    /// </summary>
    public ViewCubeOrbit Orbit { get; } = orbit;
}
