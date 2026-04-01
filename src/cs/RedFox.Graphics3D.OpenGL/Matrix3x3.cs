namespace RedFox.Graphics3D.OpenGL;

public readonly struct Matrix3x3(
    float m11, float m12, float m13,
    float m21, float m22, float m23,
    float m31, float m32, float m33)
{
    public float M11 { get; } = m11;
    public float M12 { get; } = m12;
    public float M13 { get; } = m13;
    public float M21 { get; } = m21;
    public float M22 { get; } = m22;
    public float M23 { get; } = m23;
    public float M31 { get; } = m31;
    public float M32 { get; } = m32;
    public float M33 { get; } = m33;

    public static Matrix3x3 Identity { get; } = new(1, 0, 0, 0, 1, 0, 0, 0, 1);
}
