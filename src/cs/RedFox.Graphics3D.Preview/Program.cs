using Avalonia;
using Avalonia.OpenGL;
using RedFox.Graphics2D.IO;
using RedFox.Graphics3D;
using RedFox.Graphics3D.IO;

namespace RedFox.Graphics3D.Preview;

public static class Program
{
    public static int Main(string[] args)
    {
        PreviewCliOptions options;

        try
        {
            options = PreviewCliOptions.Parse(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine();
            Console.Error.WriteLine(PreviewCliOptions.GetHelpText());
            return 1;
        }

        if (options.ShowHelp)
        {
            Console.WriteLine(PreviewCliOptions.GetHelpText());
            return 0;
        }

        try
        {
            return options.Backend switch
            {
                PreviewBackend.Avalonia => RunAvaloniaPreview(args, options),
                _ => RunCliPreview(options),
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static int RunCliPreview(PreviewCliOptions options)
    {
        SceneTranslatorManager sceneTranslatorManager = TranslatorRegistry.CreateSceneTranslatorManager();
        ImageTranslatorManager imageTranslatorManager = TranslatorRegistry.CreateImageTranslatorManager();

        Scene scene = SceneBootstrapper.LoadScene(
            options.InputFiles,
            sceneTranslatorManager,
            normalizeScene: options.NormalizeScene,
            normalizeRadius: options.NormalizeRadius,
            upAxis: options.UpAxis);

        using PreviewWindow window = new(options, scene, imageTranslatorManager);
        return window.Run();
    }

    private static int RunAvaloniaPreview(string[] args, PreviewCliOptions options)
    {
        App.LaunchOptions = options;
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new Win32PlatformOptions
            {
                RenderingMode =
                [
                    Win32RenderingMode.Wgl,
                    Win32RenderingMode.AngleEgl,
                    Win32RenderingMode.Software,
                ],
                WglProfiles =
                [
                    new GlVersion(GlProfileType.OpenGL, 4, 1),
                    new GlVersion(GlProfileType.OpenGL, 3, 3),
                ],
            })
            .LogToTrace();
    }
}
