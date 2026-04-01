using RedFox.Graphics2D.IO;
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
            SceneTranslatorManager sceneTranslatorManager = TranslatorRegistry.CreateSceneTranslatorManager();
            ImageTranslatorManager imageTranslatorManager = TranslatorRegistry.CreateImageTranslatorManager();
            Scene scene = SceneBootstrapper.LoadScene(options.InputFiles, sceneTranslatorManager, options.NormalizeScene, options.NormalizeRadius, options.UpAxis);

            using PreviewWindow window = new(options, scene, imageTranslatorManager);
            return window.Run();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
