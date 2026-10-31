namespace RedFox.Graphics3D.Avalonia;

/// <summary>
/// Describes a camera orientation.
/// </summary>
/// <param name="YawRadians">The yaw.</param>
/// <param name="PitchRadians">The pitch.</param>
public readonly record struct ViewCubeOrbit(float YawRadians, float PitchRadians);
