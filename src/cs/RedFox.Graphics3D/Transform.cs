using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.Graphics3D;

/// <summary>
/// Represents a 3D transformation, including local and world position, rotation, and scale, for an object in
/// three-dimensional space.
/// </summary>
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
    /// <param name="value">The new local position represented as a <see langword="Vector3"/> structure.</param>
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
    /// <param name="value">The new local rotation represented as a <see langword="Vector3"/> structure.</param>
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
    /// <param name="value">The new world position represented as a <see langword="Vector3"/> structure.</param>
    public void SetWorldPosition(Vector3 value)
    {
        LocalPosition = null;
        WorldPosition = value;
    }

    /// <summary>
    /// Sets the world rotation of the object in 3D space.
    /// </summary>
    /// <remarks>Updating the world rotation resets the world rotation to <see langword="null"/>,
    /// indicating that the world rotation will be recalculated based on the new world rotation.</remarks>
    /// <param name="value">The new world rotation represented as a <see langword="Vector3"/> structure.</param>
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
