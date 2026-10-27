using System.Numerics;

namespace RedFox.Graphics3D.OpenGL.Resources;

internal sealed class GlUniformState(int location)
{
    public int			Location	{ get; } = location;
    public bool			HasValue	{ get; set; }
    public int			IntValue	{ get; set; }
    public Vector4		VectorValue	{ get; set; }
    public Matrix4x4	MatrixValue	{ get; set; }
}
