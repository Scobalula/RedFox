using System.Numerics;

namespace RedFox.Graphics3D.Formats.KaydaraFbx;

/// <summary>
/// Provides FBX rotation and transform math using the row-vector convention of <see cref="Matrix4x4"/>.
/// </summary>
public static class FbxTransform
{
    /// <summary>
    /// Converts Euler angles in degrees to a quaternion using the given FBX rotation order.
    /// </summary>
    /// <param name="degrees">The Euler angles in degrees.</param>
    /// <param name="order">The rotation order.</param>
    /// <returns>The equivalent quaternion.</returns>
    public static Quaternion EulerToQuaternion(Vector3 degrees, FbxRotationOrder order)
    {
        Vector3		radians	= degrees * (MathF.PI / 180.0f);
        Quaternion	x		= Quaternion.CreateFromAxisAngle(Vector3.UnitX, radians.X);
        Quaternion	y		= Quaternion.CreateFromAxisAngle(Vector3.UnitY, radians.Y);
        Quaternion	z		= Quaternion.CreateFromAxisAngle(Vector3.UnitZ, radians.Z);

        return order switch
        {
            FbxRotationOrder.EulerXZY	=> y * z * x,
            FbxRotationOrder.EulerYZX	=> x * z * y,
            FbxRotationOrder.EulerYXZ	=> z * x * y,
            FbxRotationOrder.EulerZXY	=> y * x * z,
            FbxRotationOrder.EulerZYX	=> x * y * z,
            _							=> z * y * x,
        };
    }

    /// <summary>
    /// Converts a quaternion to XYZ-order Euler angles in degrees.
    /// </summary>
    /// <param name="rotation">The rotation.</param>
    /// <returns>The Euler angles in degrees.</returns>
    public static Vector3 QuaternionToEulerXyz(Quaternion rotation)
    {
        Matrix4x4	matrix	= Matrix4x4.CreateFromQuaternion(Quaternion.Normalize(rotation));
        float		sinY	= Math.Clamp(-matrix.M13, -1.0f, 1.0f);
        float		y		= MathF.Asin(sinY);
        float		x;
        float		z;

        if (MathF.Abs(sinY) < 0.99999f)
        {
            x = MathF.Atan2(matrix.M23, matrix.M33);
            z = MathF.Atan2(matrix.M12, matrix.M11);
        }
        else
        {
            x = MathF.Atan2(-matrix.M32, matrix.M22);
            z = 0.0f;
        }

        return new Vector3(x, y, z) * (180.0f / MathF.PI);
    }

    /// <summary>
    /// Returns the XYZ Euler representation of a rotation that is closest to the previous key, avoiding flips in animation curves.
    /// </summary>
    /// <param name="rotation">The rotation.</param>
    /// <param name="previous">The Euler angles of the previous key in degrees.</param>
    /// <returns>The Euler angles in degrees.</returns>
    public static Vector3 QuaternionToEulerXyz(Quaternion rotation, Vector3 previous)
    {
        Vector3 primary		= QuaternionToEulerXyz(rotation);
        Vector3 alternate	= new(primary.X + 180.0f, 180.0f - primary.Y, primary.Z + 180.0f);

        primary		= WrapTowards(primary, previous);
        alternate	= WrapTowards(alternate, previous);

        return Vector3.DistanceSquared(primary, previous) <= Vector3.DistanceSquared(alternate, previous) ? primary : alternate;
    }

    /// <summary>
    /// Builds a scale, rotation and translation matrix.
    /// </summary>
    /// <param name="translation">The translation.</param>
    /// <param name="rotation">The rotation.</param>
    /// <param name="scale">The scale.</param>
    /// <returns>The composed matrix.</returns>
    public static Matrix4x4 Compose(Vector3 translation, Quaternion rotation, Vector3 scale)
    {
        Matrix4x4 matrix = Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromQuaternion(rotation);
        matrix.Translation = translation;
        return matrix;
    }

    /// <summary>
    /// Splits a matrix into translation, rotation and scale, tolerating negative scale and shear.
    /// </summary>
    /// <param name="matrix">The matrix to decompose.</param>
    /// <param name="translation">The translation.</param>
    /// <param name="rotation">The rotation.</param>
    /// <param name="scale">The scale.</param>
    public static void Decompose(Matrix4x4 matrix, out Vector3 translation, out Quaternion rotation, out Vector3 scale)
    {
        if (Matrix4x4.Decompose(matrix, out scale, out rotation, out translation) && float.IsFinite(rotation.W))
        {
            rotation = Quaternion.Normalize(rotation);
            return;
        }

        Vector3 x = new(matrix.M11, matrix.M12, matrix.M13);
        Vector3 y = new(matrix.M21, matrix.M22, matrix.M23);
        Vector3 z = new(matrix.M31, matrix.M32, matrix.M33);

        translation	= matrix.Translation;
        scale		= new Vector3(x.Length(), y.Length(), z.Length());

        if (Vector3.Dot(Vector3.Cross(x, y), z) < 0.0f)
        {
            scale.X	= -scale.X;
            x		= -x;
        }

        x = scale.X != 0.0f ? x / MathF.Abs(scale.X) : Vector3.UnitX;
        z = Vector3.Normalize(Vector3.Cross(x, scale.Y != 0.0f ? y : Vector3.UnitY));
        y = Vector3.Cross(z, x);

        Matrix4x4 basis = new(x.X, x.Y, x.Z, 0.0f, y.X, y.Y, y.Z, 0.0f, z.X, z.Y, z.Z, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);
        rotation = Quaternion.CreateFromRotationMatrix(basis);
        rotation = float.IsFinite(rotation.W) ? Quaternion.Normalize(rotation) : Quaternion.Identity;
    }

    /// <summary>
    /// Combines a node's local matrix with its parent's world matrix according to the FBX inherit type.
    /// </summary>
    /// <param name="local">The node's local matrix.</param>
    /// <param name="parentWorld">The parent's world matrix.</param>
    /// <param name="parentLocalScale">The parent's local scale, used by <see cref="FbxInheritType.Rrs"/>.</param>
    /// <param name="inheritType">The node's inherit type.</param>
    /// <returns>The node's world matrix.</returns>
    public static Matrix4x4 ComputeWorld(Matrix4x4 local, Matrix4x4 parentWorld, Vector3 parentLocalScale, FbxInheritType inheritType)
    {
        if (inheritType == FbxInheritType.Rrs)
        {
            Matrix4x4 compensated = local;

            compensated.Translation	= Vector3.Zero;
            compensated				*= Matrix4x4.CreateScale(SafeReciprocal(parentLocalScale));
            compensated.Translation	= local.Translation;

            return compensated * parentWorld;
        }

        if (inheritType != FbxInheritType.RrSs)
            return local * parentWorld;

        Decompose(local, out Vector3 localTranslation, out Quaternion localRotation, out Vector3 localScale);
        Decompose(parentWorld, out _, out Quaternion parentRotation, out Vector3 parentScale);

        return Compose(Vector3.Transform(localTranslation, parentWorld), Quaternion.Normalize(parentRotation * localRotation), localScale * parentScale);
    }

    private static Vector3 SafeReciprocal(Vector3 value)
    {
        return new Vector3(value.X != 0.0f ? 1.0f / value.X : 1.0f, value.Y != 0.0f ? 1.0f / value.Y : 1.0f, value.Z != 0.0f ? 1.0f / value.Z : 1.0f);
    }

    private static Vector3 WrapTowards(Vector3 value, Vector3 reference)
    {
        return new Vector3(WrapTowards(value.X, reference.X), WrapTowards(value.Y, reference.Y), WrapTowards(value.Z, reference.Z));
    }

    private static float WrapTowards(float value, float reference)
    {
        return value + MathF.Round((reference - value) / 360.0f) * 360.0f;
    }
}
