using System.Numerics;

namespace RedFox.Rendering.OpenGL;

/// <summary>
/// Cached resolved surface-material values for a single mesh draw.
/// </summary>
internal readonly record struct OpenGlSurfaceMaterial(
    Vector4 BaseColor,
    float SpecularStrength,
    float SpecularPower);
