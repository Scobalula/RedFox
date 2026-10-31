using System.Numerics;

namespace RedFox.Graphics3D.Avalonia;

/// <summary>
/// Works out how a view cube sits for a camera, what a point on it hits and where each of its regions points the camera.
/// </summary>
public static class ViewCubeGeometry
{
    private const float EdgeBoundary = 0.6f;
    private const float FaceEpsilon = 1e-3f;
    private const float PoleThreshold = 0.999f;
    private const float QuarterTurn = MathF.PI * 0.5f;

    /// <summary>
    /// Gets the cube's faces.
    /// </summary>
    public static IReadOnlyList<ViewCubeFace> Faces { get; } = [
        new ViewCubeFace(Vector3.UnitZ, Vector3.UnitX, Vector3.UnitY, "FRONT"),
        new ViewCubeFace(-Vector3.UnitZ, -Vector3.UnitX, Vector3.UnitY, "BACK"),
        new ViewCubeFace(Vector3.UnitX, -Vector3.UnitZ, Vector3.UnitY, "RIGHT"),
        new ViewCubeFace(-Vector3.UnitX, Vector3.UnitZ, Vector3.UnitY, "LEFT"),
        new ViewCubeFace(Vector3.UnitY, Vector3.UnitX, -Vector3.UnitZ, "TOP"),
        new ViewCubeFace(-Vector3.UnitY, Vector3.UnitX, Vector3.UnitZ, "BOTTOM"),
    ];

    /// <summary>
    /// Creates the rotation from the cube's axes to view space.
    /// </summary>
    /// <param name="orbit">The camera orientation.</param>
    /// <param name="frontYawRadians">The yaw of the cube's front face around the up axis.</param>
    /// <returns>The rotation.</returns>
    public static Quaternion GetCubeToView(ViewCubeOrbit orbit, float frontYawRadians)
    {
        Quaternion cubeToWorld	= Quaternion.CreateFromAxisAngle(Vector3.UnitY, frontYawRadians);
        Quaternion camera		= Quaternion.CreateFromYawPitchRoll(orbit.YawRadians, orbit.PitchRadians, 0.0f);

        return Quaternion.Normalize(Quaternion.Concatenate(cubeToWorld, Quaternion.Inverse(camera)));
    }

    /// <summary>
    /// Finds the orientation that looks at a region, keeping the nearest yaw so the camera takes the short way round.
    /// </summary>
    /// <param name="region">The region.</param>
    /// <param name="current">The current camera orientation.</param>
    /// <param name="frontYawRadians">The yaw of the cube's front face around the up axis.</param>
    /// <returns>The orientation.</returns>
    public static ViewCubeOrbit GetOrbit(ViewCubeRegion region, ViewCubeOrbit current, float frontYawRadians)
    {
        Vector3 direction	= Vector3.Transform(region.Direction, Quaternion.CreateFromAxisAngle(Vector3.UnitY, frontYawRadians));
        float pitch			= -MathF.Asin(Math.Clamp(direction.Y, -1.0f, 1.0f));

        if (MathF.Abs(direction.Y) > PoleThreshold)
        {
            float snappedYaw = frontYawRadians + (MathF.Round((current.YawRadians - frontYawRadians) / QuarterTurn) * QuarterTurn);
            return new ViewCubeOrbit(snappedYaw, pitch);
        }

        float yaw = MathF.Atan2(direction.X, direction.Z);
        return new ViewCubeOrbit(yaw + (MathF.Round((current.YawRadians - yaw) / MathF.Tau) * MathF.Tau), pitch);
    }

    /// <summary>
    /// Finds the orientation a number of quarter turns from the nearest square view.
    /// </summary>
    /// <param name="current">The current camera orientation.</param>
    /// <param name="frontYawRadians">The yaw of the cube's front face around the up axis.</param>
    /// <param name="yawQuarters">The quarter turns around the up axis.</param>
    /// <param name="pitchQuarters">The quarter turns over the top.</param>
    /// <returns>The orientation.</returns>
    public static ViewCubeOrbit GetTurn(ViewCubeOrbit current, float frontYawRadians, int yawQuarters, int pitchQuarters)
    {
        float yaw	= MathF.Round((current.YawRadians - frontYawRadians) / QuarterTurn) + yawQuarters;
        float pitch	= MathF.Round(current.PitchRadians / QuarterTurn) + pitchQuarters;

        return new ViewCubeOrbit(frontYawRadians + (yaw * QuarterTurn), pitch * QuarterTurn);
    }

    /// <summary>
    /// Finds the region at a point on the cube.
    /// </summary>
    /// <param name="point">The point, in cube half-widths from its centre with Y up.</param>
    /// <param name="cubeToView">The rotation from <see cref="GetCubeToView"/>.</param>
    /// <returns>The region, or <see langword="null"/> when the point misses the cube.</returns>
    public static ViewCubeRegion? HitTest(Vector2 point, Quaternion cubeToView)
    {
        Quaternion viewToCube = Quaternion.Inverse(cubeToView);

        foreach (ViewCubeFace face in Faces)
        {
            Vector3 normal = Vector3.Transform(face.Normal, cubeToView);
            if (normal.Z <= FaceEpsilon)
            {
                continue;
            }

            float depth		= (1.0f - (normal.X * point.X) - (normal.Y * point.Y)) / normal.Z;
            Vector3 local	= Vector3.Transform(new Vector3(point, depth), viewToCube);

            if (MathF.Abs(local.X) <= 1.0f + FaceEpsilon && MathF.Abs(local.Y) <= 1.0f + FaceEpsilon && MathF.Abs(local.Z) <= 1.0f + FaceEpsilon)
            {
                return new ViewCubeRegion(GetComponent(local.X, face.Normal.X), GetComponent(local.Y, face.Normal.Y), GetComponent(local.Z, face.Normal.Z));
            }
        }

        return null;
    }

    /// <summary>
    /// Finds the part of a face a region covers.
    /// </summary>
    /// <param name="region">The region.</param>
    /// <param name="face">The face.</param>
    /// <param name="corners">The patch's four corners on the cube.</param>
    /// <returns><see langword="true"/> when the region touches the face.</returns>
    public static bool TryGetPatch(ViewCubeRegion region, ViewCubeFace face, out Vector3[] corners)
    {
        corners = [];

        Vector3 signs = new(region.X, region.Y, region.Z);
        if (Vector3.Dot(signs, face.Normal) <= 0.0f)
        {
            return false;
        }

        (float rightStart, float rightEnd)	= GetPatchRange(Vector3.Dot(signs, face.Right));
        (float upStart, float upEnd)		= GetPatchRange(Vector3.Dot(signs, face.Up));

        corners = [
            face.Normal + (face.Right * rightStart) + (face.Up * upStart),
            face.Normal + (face.Right * rightEnd) + (face.Up * upStart),
            face.Normal + (face.Right * rightEnd) + (face.Up * upEnd),
            face.Normal + (face.Right * rightStart) + (face.Up * upEnd),
        ];
        return true;
    }

    private static int GetComponent(float value, float faceNormal)
    {
        if (faceNormal != 0.0f)
        {
            return Math.Sign(faceNormal);
        }

        return MathF.Abs(value) > EdgeBoundary ? Math.Sign(value) : 0;
    }

    private static (float Start, float End) GetPatchRange(float sign)
    {
        if (sign == 0.0f)
        {
            return (-EdgeBoundary, EdgeBoundary);
        }

        return sign > 0.0f ? (EdgeBoundary, 1.0f) : (-1.0f, -EdgeBoundary);
    }
}
