using System.Numerics;

namespace RedFox.Graphics3D.XAsset;

internal static class XAssetTransform
{
    public static Quaternion CreateRotation(Vector3 x, Vector3 y, Vector3 z)
    {
        Matrix4x4 matrix = new(x.X, x.Y, x.Z, 0f, y.X, y.Y, y.Z, 0f, z.X, z.Y, z.Z, 0f, 0f, 0f, 0f, 1f);
        Quaternion rotation = Quaternion.CreateFromRotationMatrix(matrix);
        return rotation.LengthSquared() > 0f ? Quaternion.Normalize(rotation) : Quaternion.Identity;
    }
}
