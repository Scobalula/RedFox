using System.Numerics;

namespace RedFox.Graphics3D.OpenGL;

/// <summary>
/// Represents a 3x3 matrix used for normal-vector transformations in shaders.
/// </summary>
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

    /// <summary>
    /// The 3x3 identity matrix.
    /// </summary>
    public static Matrix3x3 Identity { get; } = new(1, 0, 0, 0, 1, 0, 0, 0, 1);

    /// <summary>
    /// Computes the normal matrix (inverse-transpose of the upper-left 3x3) from a model matrix.
    /// Falls back to <see cref="Identity"/> when the matrix is not invertible.
    /// </summary>
    public static Matrix3x3 FromModelMatrix(Matrix4x4 model)
    {
        if (!Matrix4x4.Invert(model, out Matrix4x4 inverseModel))
            return Identity;

        Matrix4x4 transposed = Matrix4x4.Transpose(inverseModel);
        return new Matrix3x3(
            transposed.M11, transposed.M12, transposed.M13,
            transposed.M21, transposed.M22, transposed.M23,
            transposed.M31, transposed.M32, transposed.M33);
    }
}
