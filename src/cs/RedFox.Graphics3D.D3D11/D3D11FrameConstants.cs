using System.Numerics;

namespace RedFox.Graphics3D.D3D11;

internal struct D3D11FrameConstants
{
    public Matrix4x4 Model;
    public Matrix4x4 SceneAxis;
    public Matrix4x4 View;
    public Matrix4x4 Projection;
}
