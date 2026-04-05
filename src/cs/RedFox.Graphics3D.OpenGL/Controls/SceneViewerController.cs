namespace RedFox.Graphics3D.OpenGL.Controls;

public sealed class SceneViewerController
{
    public event EventHandler? FitToSceneRequested;
    public event EventHandler? ResetCameraRequested;

    public void FitToScene() => FitToSceneRequested?.Invoke(this, EventArgs.Empty);

    public void ResetCamera() => ResetCameraRequested?.Invoke(this, EventArgs.Empty);
}
