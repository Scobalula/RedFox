using System.Numerics;
using RedFox.Graphics3D;

namespace RedFox.Tests.Graphics3D;

public sealed class LightTests
{
    [Fact]
    public void Position_GetsAndSetsWorldSpaceUnderScaledParent()
    {
        Group parent = new("Parent");
        parent.BindTransform.LocalPosition = new Vector3(10f, 0f, 0f);
        parent.BindTransform.Scale = new Vector3(2f);
        Light light = parent.AddNode(new Light("Light"));
        light.BindTransform.LocalPosition = new Vector3(1f, 0f, 0f);

        Assert.Equal(new Vector3(12f, 0f, 0f), light.Position);

        light.Position = new Vector3(20f, 0f, 0f);

        Assert.Equal(new Vector3(20f, 0f, 0f), light.Position);
        Assert.Equal(new Vector3(5f, 0f, 0f), light.GetBindLocalPosition());
    }
}
