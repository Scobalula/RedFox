using RedFox.Graphics3D.OpenGL;
using RedFox.Graphics3D.OpenGL.Cameras;

namespace RedFox.Graphics3D.Preview;

public enum PreviewBackend
{
    Cli,
    Avalonia
}

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
    public bool ShowSkybox { get; set; } = true;
    public bool AutoFitOnLoad { get; set; } = true;
    public bool NormalizeScene { get; set; }
    public float NormalizeRadius { get; set; } = 10.0f;
    public bool ShowHelp { get; set; }
    public float AnimationSpeed { get; set; } = 1.0f;
    public string? AnimationName { get; set; }
    public CameraMode CameraMode { get; set; } = CameraMode.Arcball;
    public SceneUpAxis UpAxis { get; set; } = SceneUpAxis.Y;
    public string? EnvironmentMapPath { get; set; }
    public float EnvironmentMapExposure { get; set; } = 1.0f;
    public float EnvironmentMapReflectionIntensity { get; set; } = 1.0f;
    public bool EnvironmentMapBlur { get; set; }
    public float EnvironmentMapBlurRadius { get; set; } = 4.0f;
    public EnvironmentMapFlipMode EnvironmentMapFlipMode { get; set; } = EnvironmentMapFlipMode.Auto;
    public bool EnableIBL { get; set; } = true;
    public RendererShadingMode ShadingMode { get; set; } = RendererShadingMode.Pbr;
    public int MsaaSamples { get; set; } = 4;
    public PreviewBackend Backend { get; set; } = PreviewBackend.Cli;

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

                case "--backend":
                    options.Backend = ParseBackend(ParseString(args, ++i, arg));
                    break;

                case "--cli":
                    options.Backend = PreviewBackend.Cli;
                    break;

                case "--avalonia":
                    options.Backend = PreviewBackend.Avalonia;
                    break;

                case "--wireframe":
                    options.Wireframe = true;
                    break;

                case "--no-skybox":
                    options.ShowSkybox = false;
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

                case "--envmap":
                    options.EnvironmentMapPath = ParseString(args, ++i, arg);
                    break;

                case "--envmap-exposure":
                    options.EnvironmentMapExposure = ParseFloat(args, ++i, arg);
                    break;

                case "--envmap-intensity":
                    options.EnvironmentMapReflectionIntensity = ParseFloat(args, ++i, arg);
                    break;

                case "--envmap-blur":
                    options.EnvironmentMapBlur = true;
                    break;

                case "--envmap-blur-radius":
                    options.EnvironmentMapBlurRadius = ParseFloat(args, ++i, arg);
                    break;

                case "--envmap-flip-y":
                    if (options.EnvironmentMapFlipMode == EnvironmentMapFlipMode.ForceNoFlipY)
                        throw new ArgumentException("Cannot specify both '--envmap-flip-y' and '--envmap-no-flip-y'.");
                    options.EnvironmentMapFlipMode = EnvironmentMapFlipMode.ForceFlipY;
                    break;

                case "--envmap-no-flip-y":
                    if (options.EnvironmentMapFlipMode == EnvironmentMapFlipMode.ForceFlipY)
                        throw new ArgumentException("Cannot specify both '--envmap-flip-y' and '--envmap-no-flip-y'.");
                    options.EnvironmentMapFlipMode = EnvironmentMapFlipMode.ForceNoFlipY;
                    break;

                case "--no-ibl":
                    options.EnableIBL = false;
                    break;

                case "--fullbright":
                    options.ShadingMode = RendererShadingMode.Fullbright;
                    break;

                case "--shading":
                    options.ShadingMode = ParseShadingMode(ParseString(args, ++i, arg));
                    break;

                case "--msaa":
                    options.MsaaSamples = ParseInt(args, ++i, arg);
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

        if (options.MsaaSamples < 0)
            throw new ArgumentOutOfRangeException(nameof(args), "MSAA sample count cannot be negative.");

        return options;
    }

    public static string GetHelpText()
    {
        return """
            RedFox Graphics3D Preview

            Usage:
              RedFox.Graphics3D.Preview [options] <file1> [file2 ...]

            Options:
              --backend <name>       cli | avalonia. Default: cli
              --cli                  Shortcut for '--backend cli'.
              --avalonia             Shortcut for '--backend avalonia'.
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
              --no-skybox            Hide the background skybox while keeping env lighting.
              --shading <mode>       pbr | fullbright
              --fullbright           Shortcut for '--shading fullbright'.
              --no-bones              Disable skeleton bone preview lines.
              --no-grid               Disable the world grid.
              --envmap <path>         Load an equirectangular environment map.
              --envmap-exposure <v>   Environment map exposure. Default: 1.0
              --envmap-intensity <v>  Environment map reflection intensity. Default: 1.0
              --envmap-blur           Enable environment map blur.
              --envmap-blur-radius <v> Skybox blur mip level. Default: 4.0
              --envmap-flip-y         Force vertical flip for environment map import.
              --envmap-no-flip-y      Disable vertical flip for environment map import.
              --no-ibl                Disable Image-Based Lighting.
              --msaa <samples>        Use renderer-managed MSAA. Default: 4 (0 or 1 disables)
              -h, --help              Show this help text.

            Runtime shortcuts:
              1 / 2 / 3               Switch Arcball / Blender / FPS camera.
              B                       Toggle bone preview.
              H                       Toggle skybox visibility.
              L                       Toggle PBR / fullbright shading.
              V                       Toggle environment map blur.
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

    private static PreviewBackend ParseBackend(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "cli" => PreviewBackend.Cli,
            "avalonia" or "ui" => PreviewBackend.Avalonia,
            _ => throw new ArgumentException($"Unknown preview backend '{value}'."),
        };
    }

    private static RendererShadingMode ParseShadingMode(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "pbr" => RendererShadingMode.Pbr,
            "fullbright" or "unlit" or "flat" => RendererShadingMode.Fullbright,
            _ => throw new ArgumentException($"Unknown shading mode '{value}'."),
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
