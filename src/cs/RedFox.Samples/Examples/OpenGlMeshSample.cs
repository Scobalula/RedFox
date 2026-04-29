using RedFox.Graphics3D.OpenGL;

namespace RedFox.Samples.Examples;

/// <summary>
/// Runs a minimal OpenGL mesh rendering demo using the RedFox scene graph.
/// </summary>
internal sealed class OpenGlMeshSample : ISample
{
    /// <inheritdoc />
    public string Name => "graphics3d-opengl-mesh";

    /// <inheritdoc />
    public string Description => "Opens a window and renders a lit untextured mesh scene from one or more supported translator files (or a fallback triangle).";

    /// <inheritdoc />
    public int Run(string[] arguments) => SilkMeshSampleRunner.Run(arguments, "RedFox OpenGL Mesh Sample", "OpenGL Scene", new OpenGlSilkPresenterFactory());
}
