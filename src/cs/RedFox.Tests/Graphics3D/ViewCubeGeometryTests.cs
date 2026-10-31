using System.Numerics;
using RedFox.Graphics3D.Avalonia;

namespace RedFox.Tests.Graphics3D;

public sealed class ViewCubeGeometryTests
{
    private const float Precision = 1e-3f;

    private static readonly ViewCubeOrbit Start = new(0.3f, -0.2f);

    [Fact]
    public void ThereAreTwentySixRegions()
    {
        Assert.Equal(26, ViewCubeRegion.All.Count);
        Assert.Equal(26, ViewCubeRegion.All.Distinct().Count());
    }

    [Theory]
    [InlineData(0.0f)]
    [InlineData(1.5707964f)]
    [InlineData(-2.1f)]
    public void LookingAtARegionPutsItInTheMiddleOfTheCube(float frontYaw)
    {
        foreach (ViewCubeRegion region in ViewCubeRegion.All)
        {
            ViewCubeOrbit orbit = ViewCubeGeometry.GetOrbit(region, Start, frontYaw);

            Assert.Equal(region, ViewCubeGeometry.HitTest(Vector2.Zero, ViewCubeGeometry.GetCubeToView(orbit, frontYaw)));
        }
    }

    [Fact]
    public void TheFrontViewFacesAlongTheFrontYaw()
    {
        ViewCubeOrbit orbit = ViewCubeGeometry.GetOrbit(new ViewCubeRegion(0, 0, 1), new ViewCubeOrbit(0.0f, 0.0f), 0.7f);

        Assert.Equal(0.7f, orbit.YawRadians, Precision);
        Assert.Equal(0.0f, orbit.PitchRadians, Precision);
    }

    [Fact]
    public void TheTopViewLooksDown()
    {
        ViewCubeOrbit orbit = ViewCubeGeometry.GetOrbit(new ViewCubeRegion(0, 1, 0), Start, 0.0f);

        Assert.Equal(-MathF.PI * 0.5f, orbit.PitchRadians, Precision);
    }

    [Fact]
    public void PoleViewsSnapToTheNearestQuarterTurn()
    {
        ViewCubeOrbit orbit = ViewCubeGeometry.GetOrbit(new ViewCubeRegion(0, -1, 0), new ViewCubeOrbit(1.4f, 0.0f), 0.0f);

        Assert.Equal(MathF.PI * 0.5f, orbit.YawRadians, Precision);
    }

    [Fact]
    public void TheCameraTakesTheShortWayRound()
    {
        ViewCubeOrbit orbit = ViewCubeGeometry.GetOrbit(new ViewCubeRegion(0, 0, 1), new ViewCubeOrbit(MathF.Tau * 3.0f + 0.2f, 0.0f), 0.0f);

        Assert.Equal(MathF.Tau * 3.0f, orbit.YawRadians, Precision);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(-1, 0)]
    [InlineData(0, 1)]
    [InlineData(0, -1)]
    public void ATurnMovesAQuarterFromTheFront(int yawQuarters, int pitchQuarters)
    {
        ViewCubeOrbit orbit = ViewCubeGeometry.GetTurn(new ViewCubeOrbit(0.0f, 0.0f), 0.0f, yawQuarters, pitchQuarters);

        Assert.Equal(yawQuarters * MathF.PI * 0.5f, orbit.YawRadians, Precision);
        Assert.Equal(pitchQuarters * MathF.PI * 0.5f, orbit.PitchRadians, Precision);
    }

    [Fact]
    public void ATurnSquaresUpAnAngledView()
    {
        ViewCubeOrbit orbit = ViewCubeGeometry.GetTurn(new ViewCubeOrbit(0.3f, -0.2f), 0.0f, 1, 0);

        Assert.Equal(MathF.PI * 0.5f, orbit.YawRadians, Precision);
        Assert.Equal(0.0f, orbit.PitchRadians, Precision);
    }

    [Fact]
    public void ATurnFollowsTheFrontYaw()
    {
        ViewCubeOrbit orbit = ViewCubeGeometry.GetTurn(new ViewCubeOrbit(0.7f, 0.0f), 0.7f, 1, 0);

        Assert.Equal(0.7f + (MathF.PI * 0.5f), orbit.YawRadians, Precision);
    }

    [Fact]
    public void PointsOffTheCubeMiss()
    {
        Assert.Null(ViewCubeGeometry.HitTest(new Vector2(1.5f, 0.0f), ViewCubeGeometry.GetCubeToView(new ViewCubeOrbit(0.0f, 0.0f), 0.0f)));
    }

    [Theory]
    [InlineData(0.0f, 0.0f, 0, 0, 1)]
    [InlineData(0.9f, 0.0f, 1, 0, 1)]
    [InlineData(0.0f, 0.9f, 0, 1, 1)]
    [InlineData(-0.9f, -0.9f, -1, -1, 1)]
    public void TheFrontFaceSplitsIntoNineRegions(float x, float y, int regionX, int regionY, int regionZ)
    {
        Quaternion cubeToView = ViewCubeGeometry.GetCubeToView(new ViewCubeOrbit(0.0f, 0.0f), 0.0f);

        Assert.Equal(new ViewCubeRegion(regionX, regionY, regionZ), ViewCubeGeometry.HitTest(new Vector2(x, y), cubeToView));
    }

    [Fact]
    public void ACornerTouchesThreeFaces()
    {
        ViewCubeRegion corner = new(1, 1, 1);

        Assert.Equal(3, ViewCubeGeometry.Faces.Count(face => ViewCubeGeometry.TryGetPatch(corner, face, out _)));
    }

    [Fact]
    public void AFaceRegionPatchSitsOnItsFace()
    {
        ViewCubeFace front = ViewCubeGeometry.Faces[0];

        Assert.True(ViewCubeGeometry.TryGetPatch(new ViewCubeRegion(0, 0, 1), front, out Vector3[] corners));
        Assert.All(corners, corner => Assert.Equal(1.0f, corner.Z, Precision));
        Assert.False(ViewCubeGeometry.TryGetPatch(new ViewCubeRegion(0, 0, -1), front, out _));
    }
}
