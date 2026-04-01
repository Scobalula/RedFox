using RedFox.Graphics3D.OpenGL.Cameras;

namespace RedFox.Graphics3D.Preview;

public enum SceneUpAxis
{
    Y,
    Z,
    X
}

public sealed class PreviewCliOptions
{
    public List<string> InputFiles { get; } = [];
    public int Width { get; set; } = 1280;
    public int Height { get; set; } = 720;
    public int MaxFrames { get; set; }
    public bool Hidden { get; set; }
    public bool ShowBones { get; set; } = true;
    public bool ShowGrid { get; set; } = true;
    public bool Wireframe { get; set; }
    public bool AutoFitOnLoad { get; set; } = true;
    public bool NormalizeScene { get; set; }
    public float NormalizeRadius { get; set; } = 10.0f;
    public bool ShowHelp { get; set; }
    public float AnimationSpeed { get; set; } = 1.0f;
    public string? AnimationName { get; set; }
    public CameraMode CameraMode { get; set; } = CameraMode.Arcball;
    public SceneUpAxis UpAxis { get; set; } = SceneUpAxis.Y;

    public static PreviewCliOptions Parse(IReadOnlyList<string> args)
    {
        PreviewCliOptions options = new();

        for (int i = 0; i < args.Count; i++)
        {
            string arg = args[i];

            if (string.IsNullOrWhiteSpace(arg))
                continue;

            if (!arg.StartsWith('-'))
            {
                options.InputFiles.Add(Path.GetFullPath(arg));
                continue;
            }

            switch (arg)
            {
                case "-h":
                case "--help":
                    options.ShowHelp = true;
                    break;

                case "--hidden":
                    options.Hidden = true;
                    break;

                case "--wireframe":
                    options.Wireframe = true;
                    break;

                case "--no-bones":
                    options.ShowBones = false;
                    break;

                case "--no-grid":
                    options.ShowGrid = false;
                    break;

                case "--no-fit":
                    options.AutoFitOnLoad = false;
                    break;

                case "--normalize-scene":
                    options.NormalizeScene = true;
                    break;

                case "--normalize-radius":
                    options.NormalizeRadius = ParseFloat(args, ++i, arg);
                    break;

                case "--frames":
                    options.MaxFrames = ParseInt(args, ++i, arg);
                    break;

                case "--width":
                    options.Width = ParseInt(args, ++i, arg);
                    break;

                case "--height":
                    options.Height = ParseInt(args, ++i, arg);
                    break;

                case "--speed":
                    options.AnimationSpeed = ParseFloat(args, ++i, arg);
                    break;

                case "--animation":
                    options.AnimationName = ParseString(args, ++i, arg);
                    break;

                case "--camera":
                case "--camera-mode":
                    options.CameraMode = ParseCameraMode(ParseString(args, ++i, arg));
                    break;

                case "--up-axis":
                    options.UpAxis = ParseUpAxis(ParseString(args, ++i, arg));
                    break;

                default:
                    throw new ArgumentException($"Unknown argument '{arg}'.");
            }
        }

        if (!options.ShowHelp && options.InputFiles.Count == 0)
            throw new ArgumentException("At least one input file must be supplied.");

        if (options.Width <= 0 || options.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(args), "Window size must be positive.");

        if (options.AnimationSpeed <= 0f)
            throw new ArgumentOutOfRangeException(nameof(args), "Animation speed must be greater than zero.");

        if (options.NormalizeRadius <= 0f)
            throw new ArgumentOutOfRangeException(nameof(args), "Normalize radius must be greater than zero.");

        return options;
    }

    public static string GetHelpText()
    {
        return """
            RedFox Graphics3D Preview

            Usage:
              RedFox.Graphics3D.Preview [options] <file1> [file2 ...]

            Options:
              --hidden                Create the preview window hidden.
              --frames <count>        Auto-close after N rendered frames.
              --width <pixels>        Window width. Default: 1280
              --height <pixels>       Window height. Default: 720
              --camera <mode>         arcball | blender | fps
              --up-axis <axis>        y | z | x. Default: y
              --animation <name>      Only play animations with the supplied name.
              --speed <value>         Animation playback speed. Default: 1.0
              --no-fit                Keep the current camera placement instead of fitting to bounds.
              --normalize-scene       Uniformly scale loaded content to a target radius.
              --normalize-radius <v>  Target radius for normalization. Default: 10
              --wireframe             Render in wireframe mode.
              --no-bones              Disable skeleton bone preview lines.
              --no-grid               Disable the world grid.
              -h, --help              Show this help text.

            Runtime shortcuts:
              1 / 2 / 3               Switch Arcball / Blender / FPS camera.
              B                       Toggle bone preview.
              G                       Toggle grid.
              W                       Toggle wireframe.
              F                       Fit the camera to the loaded scene.
              Escape                  Close the preview window.
            """;
    }

    private static int ParseInt(IReadOnlyList<string> args, int index, string option)
    {
        string value = ParseString(args, index, option);
        return int.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static float ParseFloat(IReadOnlyList<string> args, int index, string option)
    {
        string value = ParseString(args, index, option);
        return float.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string ParseString(IReadOnlyList<string> args, int index, string option)
    {
        if ((uint)index >= (uint)args.Count)
            throw new ArgumentException($"Missing value for '{option}'.");

        return args[index];
    }

    private static CameraMode ParseCameraMode(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "arcball" => CameraMode.Arcball,
            "blender" => CameraMode.Blender,
            "fps" => CameraMode.Fps,
            _ => throw new ArgumentException($"Unknown camera mode '{value}'."),
        };
    }

    private static SceneUpAxis ParseUpAxis(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "y" or "y-up" => SceneUpAxis.Y,
            "z" or "z-up" => SceneUpAxis.Z,
            "x" or "x-up" => SceneUpAxis.X,
            _ => throw new ArgumentException($"Unknown up-axis '{value}'."),
        };
    }
}
