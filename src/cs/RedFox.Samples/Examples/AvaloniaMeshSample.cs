using Avalonia;
using Avalonia.OpenGL;
using Avalonia.Win32;

namespace RedFox.Samples.Examples;

/// <summary>
/// Runs the Avalonia OpenGL mesh rendering demo using the RedFox scene graph.
/// </summary>
internal sealed class AvaloniaMeshSample : ISample
{
    /// <inheritdoc />
    public string Name => "graphics3d-avalonia-opengl-mesh";

    /// <inheritdoc />
    public string Description => "Opens an Avalonia window with a bindable OpenGL scene renderer, scene tree, and file picker.";

    /// <inheritdoc />
    public int Run(string[] arguments)
    {
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(arguments);
    }

    private static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<AvaloniaSampleApp>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace().With(new AngleOptions
            {
                GlProfiles = new[] { new GlVersion(GlProfileType.OpenGLES, 3, 1) }
            });
    }
}
