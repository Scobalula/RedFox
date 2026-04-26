using System.Numerics;

namespace RedFox.Graphics3D.D3D11;

internal struct D3D11GridFrameConstants
{
    public Matrix4x4 View;
    public Matrix4x4 Projection;
    public Vector4 CameraAndGridSize;
}

internal struct D3D11GridStyleConstants
{
    public Vector4 CellAndLine;
    public Vector4 MinorColor;
    public Vector4 MajorColor;
    public Vector4 AxisXColor;
    public Vector4 AxisZColor;
    public Vector4 Fade;
}