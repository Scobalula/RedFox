using System.Numerics;

namespace RedFox.Graphics3D.Rendering;

/// <summary>
/// Describes the layout of a baked irradiance probe grid in scene-axis world space.
/// </summary>
/// <param name="Origin">The position of the first probe.</param>
/// <param name="Spacing">The distance between neighbouring probes on each axis.</param>
/// <param name="CountX">The number of probes along X.</param>
/// <param name="CountY">The number of probes along Y.</param>
/// <param name="CountZ">The number of probes along Z.</param>
/// <param name="DistanceScale">The world distance that probe hit distances are normalized by.</param>
public readonly record struct IrradianceProbeGrid(Vector3 Origin, Vector3 Spacing, int CountX, int CountY, int CountZ, float DistanceScale);
