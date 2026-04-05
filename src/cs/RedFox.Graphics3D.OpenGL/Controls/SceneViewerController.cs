namespace RedFox.Graphics3D.OpenGL.Controls;

/// <summary>
/// Exposes commands that request the scene viewer to adjust the camera.
/// </summary>
public sealed class SceneViewerController
{
    /// <summary>
    /// Raised when <see cref="FitToScene"/> is called.
    /// </summary>
    public event EventHandler? FitToSceneRequested;

    /// <summary>
    /// Raised when <see cref="ResetCamera"/> is called.
    /// </summary>
    public event EventHandler? ResetCameraRequested;

    /// <summary>
    /// Requests the viewer to frame all scene content.
    /// </summary>
    public void FitToScene() => FitToSceneRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Requests the viewer to reset the camera to its default position and orientation.
    /// </summary>
    public void ResetCamera() => ResetCameraRequested?.Invoke(this, EventArgs.Empty);
}
