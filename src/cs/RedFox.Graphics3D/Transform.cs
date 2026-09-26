using System.Numerics;

namespace RedFox.Graphics3D;

/// <summary>
/// Holds the authored translation, rotation, and scale of a <see cref="SceneNode"/>.
/// </summary>
/// <remarks>
/// <para>
/// Each value is optional. A set value is authoritative; an unset value is derived on demand by
/// <see cref="SceneNode"/> from the other space and the parent's pose, and is never written back.
/// When both the local and world value of a component are set, local-space queries use the local
/// value and world-space queries use the world value.
/// </para>
/// <para>
/// Conventions: <c>System.Numerics</c> row vectors, so a local matrix is composed as
/// <c>Scale * Rotation * Translation</c> and a world matrix as <c>Local * ParentWorld</c>. World
/// rotation is <c>ParentWorldRotation * LocalRotation</c>. World position and rotation are a rigid
/// composition that does not apply ancestor scale; scale is local and only affects matrices.
/// Handedness, up axis, and units are whatever the source data uses; see <see cref="Scene.UpAxis"/>.
/// </para>
/// </remarks>
public class Transform
{
    /// <summary>
    /// Gets or sets the local position of the object in three-dimensional space, usually relative to the parent.
    /// </summary>
    /// <remarks>If the value is null, it indicates that the value is not defined and must be computed.</remarks>
    public Vector3? LocalPosition { get; set; }

    /// <summary>
    /// Gets or sets the local rotation of the object in three-dimensional space, usually relative to the parent.
    /// </summary>
    /// <remarks>If the value is null, it indicates that the value is not defined and must be computed.</remarks>
    public Quaternion? LocalRotation { get; set; }

    /// <summary>
    /// Gets or sets the world position of the object in three-dimensional space, usually relative to 0,0,0.
    /// </summary>
    /// <remarks>If the value is null, it indicates that the value is not defined and must be computed.</remarks>
    public Vector3? WorldPosition { get; set; }

    /// <summary>
    /// Gets or sets the world rotation of the object in three-dimensional space, usually relative to 0,0,0.
    /// </summary>
    /// <remarks>If the value is null, it indicates that the value is not defined and must be computed.</remarks>
    public Quaternion? WorldRotation { get; set; }

    /// <summary>
    /// Gets or sets the scale factor on all axis.
    /// </summary>
    /// <remarks>If the value is null, it indicates that the value is not defined.</remarks>
    public Vector3? Scale { get; set; }

    /// <summary>
    /// Creates a copy of this transform.
    /// </summary>
    /// <returns>A new <see cref="Transform"/> with the same values.</returns>
    public Transform Clone() => (Transform)MemberwiseClone();

    /// <summary>
    /// Copies the local and world transformation properties from the current instance to the specified <see
    /// cref="Transform"/> object.
    /// </summary>
    public void CopyTo(Transform other)
    {
        other.LocalPosition = LocalPosition;
        other.LocalRotation = LocalRotation;
        other.WorldPosition = WorldPosition;
        other.WorldRotation = WorldRotation;
        other.Scale = Scale;
    }

    /// <summary>
    /// Sets the local position of the object in 3D space.
    /// </summary>
    /// <remarks>Updating the local position resets the world position to <see langword="null"/>,
    /// indicating that the world position will be recalculated based on the new local position.</remarks>
    /// <param name="value">The new local position.</param>
    public void SetLocalPosition(Vector3 value)
    {
        LocalPosition = value;
        WorldPosition = null;
    }

    /// <summary>
    /// Sets the local rotation of the object in 3D space.
    /// </summary>
    /// <remarks>Updating the local rotation resets the world rotation to <see langword="null"/>,
    /// indicating that the world rotation will be recalculated based on the new local rotation.</remarks>
    /// <param name="value">The new local rotation.</param>
    public void SetLocalRotation(Quaternion value)
    {
        LocalRotation = value;
        WorldRotation = null;
    }

    /// <summary>
    /// Sets the world position of the object in 3D space.
    /// </summary>
    /// <remarks>Updating the world position resets the local position to <see langword="null"/>,
    /// indicating that the local position will be recalculated based on the new world position.</remarks>
    /// <param name="value">The new world position.</param>
    public void SetWorldPosition(Vector3 value)
    {
        LocalPosition = null;
        WorldPosition = value;
    }

    /// <summary>
    /// Sets the world rotation of the object in 3D space.
    /// </summary>
    /// <remarks>Updating the world rotation resets the local rotation to <see langword="null"/>,
    /// indicating that the local rotation will be derived from the new world rotation.</remarks>
    /// <param name="value">The new world rotation.</param>
    public void SetWorldRotation(Quaternion value)
    {
        LocalRotation = null;
        WorldRotation = value;
    }

    /// <summary>
    /// Invalidates the current transformation state by resetting all transformation-related properties to an undefined
    /// state.
    /// </summary>
    public void Invalidate()
    {
        LocalPosition = null;
        LocalRotation = null;
        WorldPosition = null;
        WorldRotation = null;
        Scale = null;
    }
}
