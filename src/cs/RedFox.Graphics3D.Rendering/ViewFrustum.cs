using System.Numerics;

namespace RedFox.Graphics3D.Rendering;

internal readonly struct ViewFrustum(Plane left, Plane right, Plane bottom, Plane top, Plane near, Plane far)
{
    private readonly Plane	_left	= left;
    private readonly Plane	_right	= right;
    private readonly Plane	_bottom	= bottom;
    private readonly Plane	_top	= top;
    private readonly Plane	_near	= near;
    private readonly Plane	_far	= far;

    public static ViewFrustum FromViewProjection(in Matrix4x4 viewProjection)
    {
        Vector4 x = new(viewProjection.M11, viewProjection.M21, viewProjection.M31, viewProjection.M41);
        Vector4 y = new(viewProjection.M12, viewProjection.M22, viewProjection.M32, viewProjection.M42);
        Vector4 z = new(viewProjection.M13, viewProjection.M23, viewProjection.M33, viewProjection.M43);
        Vector4 w = new(viewProjection.M14, viewProjection.M24, viewProjection.M34, viewProjection.M44);

        return new ViewFrustum(new Plane(w + x), new Plane(w - x), new Plane(w + y), new Plane(w - y), new Plane(w + z), new Plane(w - z));
    }

    public bool Intersects(in SceneBounds bounds)
    {
        if (!bounds.IsValid)
        {
            return true;
        }

        return !IsOutside(_left, bounds) && !IsOutside(_right, bounds) && !IsOutside(_bottom, bounds) && !IsOutside(_top, bounds) && !IsOutside(_near, bounds) && !IsOutside(_far, bounds);
    }

    private static bool IsOutside(in Plane plane, in SceneBounds bounds)
    {
        if (plane.Normal == Vector3.Zero)
        {
            return false;
        }

        Vector3 positive = new(plane.Normal.X >= 0.0f ? bounds.Max.X : bounds.Min.X, plane.Normal.Y >= 0.0f ? bounds.Max.Y : bounds.Min.Y, plane.Normal.Z >= 0.0f ? bounds.Max.Z : bounds.Min.Z);
        return Vector3.Dot(plane.Normal, positive) + plane.D < 0.0f;
    }
}
