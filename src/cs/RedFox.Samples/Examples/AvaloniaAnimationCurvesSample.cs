using Avalonia;
using Avalonia.OpenGL;
using Avalonia.Win32;

namespace RedFox.Samples.Examples;

internal sealed class AvaloniaAnimationCurvesSample : ISample
{
    private const string DefaultAnimationPath = @"G:\Projects\Alchemist\old\Alchemist.CLI\bin\Debug\net8.0\Output\vm_sm_uzulu_reload_empty.seanim";

    /// <inheritdoc />
    public string Name => "graphics3d-avalonia-animation-curves";

    /// <inheritdoc />
    public string Description => "Opens an Avalonia skeletal animation curve viewer backed by the scene translators.";

    /// <inheritdoc />
    public int Run(string[] arguments)
    {
        string[] resolvedArguments = ResolveArguments(arguments);
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(resolvedArguments);
    }

    private static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<AvaloniaAnimationCurvesApp>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace().With(new AngleOptions
            {
                GlProfiles = new[] { new GlVersion(GlProfileType.OpenGLES, 3, 1) }
            });
    }

    private static string[] ResolveArguments(string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (arguments.Length != 0 || !File.Exists(DefaultAnimationPath))
        {
            return arguments;
        }

        return [DefaultAnimationPath];
    }
}