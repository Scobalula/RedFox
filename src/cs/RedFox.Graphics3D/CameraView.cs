using System.Numerics;

namespace RedFox.Graphics3D;

/// <summary>
/// Renderer-facing snapshot of a camera's view, projection, and world position
/// for the current frame. Renderers should consume this instead of <see cref="Camera"/>
/// so they remain decoupled from any specific camera implementation.
/// </summary>
/// <param name="viewMatrix">The world-to-view matrix.</param>
/// <param name="projectionMatrix">The view-to-clip matrix.</param>
/// <param name="position">The world-space camera position.</param>
public readonly struct CameraView(Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix, Vector3 position)
{
    /// <summary>
    /// Gets the world-to-view matrix.
    /// </summary>
    public Matrix4x4 ViewMatrix { get; } = viewMatrix;

    /// <summary>
    /// Gets the view-to-clip projection matrix.
    /// </summary>
    public Matrix4x4 ProjectionMatrix { get; } = projectionMatrix;

    /// <summary>
    /// Gets the world-space camera position.
    /// </summary>
    public Vector3 Position { get; } = position;
}
