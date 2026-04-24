namespace RedFox.Graphics3D.Rendering.Hosting;

/// <summary>
/// Represents a renderer-agnostic input source that can produce camera-controller input.
/// </summary>
public interface IInputSource
{
    /// <summary>
    /// Samples the current input state.
    /// </summary>
    /// <returns>The sampled camera-controller input for the current frame.</returns>
    CameraControllerInput ReadInput();
}