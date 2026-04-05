using System.Numerics;
using RedFox.Graphics3D.OpenGL.Viewing;

namespace RedFox.Tests.Graphics3D;

public sealed class ViewSpaceLineClipperTests
{
    [Fact]
    public void TryClipToPerspectiveDepth_LineCrossingNearPlane_IsClippedToNearPlane()
    {
        Vector3 start = new(0.0f, 0.0f, 0.0f);
        Vector3 end = new(0.0f, 0.0f, -2.0f);

        bool visible = ViewSpaceLineClipper.TryClipToPerspectiveDepth(ref start, ref end, 0.1f, 100.0f);

        Assert.True(visible);
        Assert.Equal(-0.1f, start.Z, 4);
        Assert.Equal(-2.0f, end.Z, 4);
    }

    [Fact]
    public void TryClipToPerspectiveDepth_LineEntirelyInFrontOfNearPlane_IsRejected()
    {
        Vector3 start = new(0.0f, 0.0f, 0.0f);
        Vector3 end = new(0.0f, 0.0f, -0.01f);

        bool visible = ViewSpaceLineClipper.TryClipToPerspectiveDepth(ref start, ref end, 0.1f, 100.0f);

        Assert.False(visible);
    }

    [Fact]
    public void TryClipToPerspectiveDepth_LineCrossingFarPlane_IsClippedToFarPlane()
    {
        Vector3 start = new(0.0f, 0.0f, -2.0f);
        Vector3 end = new(0.0f, 0.0f, -200.0f);

        bool visible = ViewSpaceLineClipper.TryClipToPerspectiveDepth(ref start, ref end, 0.1f, 100.0f);

        Assert.True(visible);
        Assert.Equal(-2.0f, start.Z, 4);
        Assert.Equal(-100.0f, end.Z, 4);
    }
}
