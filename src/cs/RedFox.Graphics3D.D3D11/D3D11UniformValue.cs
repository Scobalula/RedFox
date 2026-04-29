using System.Numerics;

namespace RedFox.Graphics3D.D3D11;

internal readonly record struct D3D11UniformValue(
    D3D11UniformValueKind Kind,
    int IntValue,
    float FloatValue,
    Vector2 Vector2Value,
    Vector3 Vector3Value,
    Vector4 Vector4Value,
    Matrix4x4 MatrixValue,
    Vector3[]? Vector3ArrayValue,
    Vector4[]? Vector4ArrayValue)
{
    public static D3D11UniformValue FromInt(int value)
        => new(D3D11UniformValueKind.Int, value, 0.0f, default, default, default, default, null, null);

    public static D3D11UniformValue FromFloat(float value)
        => new(D3D11UniformValueKind.Float, 0, value, default, default, default, default, null, null);

    public static D3D11UniformValue FromVector2(Vector2 value)
        => new(D3D11UniformValueKind.Vector2, 0, 0.0f, value, default, default, default, null, null);

    public static D3D11UniformValue FromVector3(Vector3 value)
        => new(D3D11UniformValueKind.Vector3, 0, 0.0f, default, value, default, default, null, null);

    public static D3D11UniformValue FromVector4(Vector4 value)
        => new(D3D11UniformValueKind.Vector4, 0, 0.0f, default, default, value, default, null, null);

    public static D3D11UniformValue FromMatrix4x4(Matrix4x4 value)
        => new(D3D11UniformValueKind.Matrix4x4, 0, 0.0f, default, default, default, value, null, null);

    public static D3D11UniformValue FromVector3Array(Vector3[] value)
        => new(D3D11UniformValueKind.Vector3Array, 0, 0.0f, default, default, default, default, value, null);

    public static D3D11UniformValue FromVector4Array(Vector4[] value)
        => new(D3D11UniformValueKind.Vector4Array, 0, 0.0f, default, default, default, default, null, value);
}