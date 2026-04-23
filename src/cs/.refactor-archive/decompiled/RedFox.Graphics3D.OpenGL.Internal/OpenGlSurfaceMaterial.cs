using System.Numerics;

namespace RedFox.Graphics3D.OpenGL.Internal;

internal readonly record struct OpenGlSurfaceMaterial(Vector4 BaseColor, float SpecularStrength, float SpecularPower);
