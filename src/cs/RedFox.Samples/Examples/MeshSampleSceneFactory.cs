using RedFox.Graphics3D;
using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.Bvh;
using RedFox.Graphics3D.Cast;
using RedFox.Graphics3D.Gltf;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.KaydaraFbx;
using RedFox.Graphics3D.MayaAscii;
using RedFox.Graphics3D.Md5;
using RedFox.Graphics3D.Rendering;
using RedFox.Graphics3D.Rendering.Hosting;
using RedFox.Graphics3D.SEAnim;
using RedFox.Graphics3D.Semodel;
using RedFox.Graphics3D.Smd;
using RedFox.Graphics3D.WavefrontObj;
using System.Globalization;
using System.Numerics;

namespace RedFox.Samples.Examples;

internal static class MeshSampleSceneFactory
{
    private const int PreviewRandomLightCount = 3;

    public static bool TryCreate(string[] arguments, string fallbackSceneName, out MeshSampleSceneContext? context, out string? error)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentException.ThrowIfNullOrWhiteSpace(fallbackSceneName);

        MeshSampleOptions options = ParseOptions(arguments);
        return TryCreate(options, fallbackSceneName, out context, out error);
    }

    public static bool TryCreate(MeshSampleOptions options, string fallbackSceneName, out MeshSampleSceneContext? context, out string? error)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(fallbackSceneName);

        if (!TryCreateScene(options.ScenePaths, fallbackSceneName, out Scene? scene, out error))
        {
            context = null;
            return false;
        }

        ArgumentNullException.ThrowIfNull(scene);
        scene.UpAxis = options.UpAxis;
        scene.FaceWinding = options.FaceWinding;

        IReadOnlyList<AnimationPlayer> animationPlayers = scene.CreateAnimationPlayers();
        OrbitCamera camera = scene.RootNode.AddNode(CreateCamera());
        SceneViewportController viewportController = new(scene, camera)
        {
            RefreshAnimatedSceneBounds = animationPlayers.Count > 0,
            IncludeNodeInBounds = ShouldIncludeInBounds,
        };

        if (viewportController.RecomputeBounds())
        {
            viewportController.FitCameraToScene();
            EnsurePreviewLights(scene, viewportController.Bounds, PreviewRandomLightCount);
        }
        else
        {
            AddFallbackLight(scene);
        }

        Grid grid = ConfigureGrid(scene.Grid, options.ShowGrid);
        context = new MeshSampleSceneContext(options, scene, camera, grid, viewportController, animationPlayers);
        error = null;
        return true;
    }

    public static MeshSampleOptions ParseOptions(string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        MeshSampleOptions options = new();

        for (int i = 0; i < arguments.Length; i++)
        {
            string arg = arguments[i];
            if (arg.Equals("--no-grid", StringComparison.OrdinalIgnoreCase))
            {
                options.ShowGrid = false;
                continue;
            }

            if (arg.Equals("--grid", StringComparison.OrdinalIgnoreCase))
            {
                options.ShowGrid = true;
                continue;
            }

            if (arg.StartsWith("--up-axis=", StringComparison.OrdinalIgnoreCase))
            {
                string value = arg[10..].Trim();
                if (value.Equals("x", StringComparison.OrdinalIgnoreCase))
                {
                    options.UpAxis = SceneUpAxis.X;
                }
                else if (value.Equals("z", StringComparison.OrdinalIgnoreCase))
                {
                    options.UpAxis = SceneUpAxis.Z;
                }
                else
                {
                    options.UpAxis = SceneUpAxis.Y;
                }

                continue;
            }

            if (arg.StartsWith("--winding=", StringComparison.OrdinalIgnoreCase))
            {
                string value = arg[10..].Trim();
                options.FaceWinding = value.Equals("cw", StringComparison.OrdinalIgnoreCase)
                    ? FaceWinding.Clockwise
                    : FaceWinding.CounterClockwise;
                continue;
            }

            if (arg.Equals("--view-lighting", StringComparison.OrdinalIgnoreCase)
                || arg.Equals("--view-lit", StringComparison.OrdinalIgnoreCase))
            {
                options.UseViewBasedLighting = true;
                continue;
            }

            if (arg.Equals("--scene-lighting", StringComparison.OrdinalIgnoreCase)
                || arg.Equals("--no-view-lighting", StringComparison.OrdinalIgnoreCase))
            {
                options.UseViewBasedLighting = false;
                continue;
            }

            if (arg.StartsWith("--skinning=", StringComparison.OrdinalIgnoreCase))
            {
                string value = arg[11..].Trim();
                options.SkinningMode = value.Equals("dual", StringComparison.OrdinalIgnoreCase)
                    || value.Equals("dualquaternion", StringComparison.OrdinalIgnoreCase)
                    || value.Equals("dq", StringComparison.OrdinalIgnoreCase)
                    ? SkinningMode.DualQuaternion
                    : SkinningMode.Linear;
                continue;
            }

            if (arg.StartsWith("--exit-after=", StringComparison.OrdinalIgnoreCase))
            {
                string value = arg[13..].Trim();
                if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedSeconds) && parsedSeconds > 0.0f)
                {
                    options.ExitAfterSeconds = parsedSeconds;
                }

                continue;
            }

            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                options.ScenePaths.Add(arg);
            }
        }

        return options;
    }

    public static bool ShouldIncludeInBounds(SceneNode node)
    {
        if (node is SkeletonBone bone && !bone.ShowSkeletonBone)
        {
            return false;
        }

        return true;
    }

    private static bool TryCreateScene(IReadOnlyList<string> scenePaths, string fallbackSceneName, out Scene? scene, out string? error)
    {
        if (scenePaths.Count == 0)
        {
            scene = CreateFallbackScene(fallbackSceneName);
            error = null;
            return true;
        }

        SceneTranslatorManager manager = CreateTranslatorManager();
        scene = new Scene(Path.GetFileName(scenePaths[0]));

        try
        {
            for (int i = 0; i < scenePaths.Count; i++)
            {
                string inputPath = Path.GetFullPath(scenePaths[i]);
                if (!File.Exists(inputPath))
                {
                    scene = null;
                    error = $"Input scene file was not found: {inputPath}";
                    return false;
                }

                SceneTranslatorOptions sceneTranslatorOptions = new();
                manager.Read(inputPath, scene, sceneTranslatorOptions);
            }

            error = null;
            return true;
        }
        catch (Exception exception)
        {
            scene = null;
            error = $"Failed to load scene input(s): {exception.Message}";
            return false;
        }
    }

    private static SceneTranslatorManager CreateTranslatorManager()
    {
        SceneTranslatorManager manager = new();
        manager.Register<ObjTranslator>();
        manager.Register<GltfTranslator>();
        manager.Register<SemodelTranslator>();
        manager.Register<SmdTranslator>();
        manager.Register<MayaAsciiTranslator>();
        manager.Register<FbxTranslator>();
        manager.Register<CastTranslator>();
        manager.Register<BvhTranslator>();
        manager.Register<Md5MeshTranslator>();
        manager.Register<Md5AnimTranslator>();
        manager.Register<SeanimTranslator>();
        return manager;
    }

    private static Scene CreateFallbackScene(string fallbackSceneName)
    {
        Scene scene = new(fallbackSceneName);
        Mesh mesh = scene.RootNode.AddNode<Mesh>("Triangle");
        mesh.Positions = CreatePositions();
        mesh.Normals = CreateNormals();
        mesh.FaceIndices = CreateIndices();

        Material material = new("TriangleMaterial")
        {
            DiffuseColor = new Vector4(0.92f, 0.3f, 0.24f, 1.0f)
        };

        mesh.Materials = new List<Material> { material };
        return scene;
    }

    private static OrbitCamera CreateCamera()
    {
        OrbitCamera camera = new("MainCamera")
        {
            AspectRatio = 1280.0f / 720.0f,
            NearPlane = 0.01f,
            FarPlane = 5000.0f,
            FieldOfView = 60.0f,
            LookSensitivity = 1.0f,
            ZoomSensitivity = 1.0f,
            PanSensitivity = 1.0f,
            MoveSpeed = 2.5f,
            BoostMultiplier = 3.0f,
            MinDistance = 0.05f,
            MaxDistance = 1000000.0f,
            UsePitchLimits = false,
            InvertX = true,
            InvertY = true
        };
        camera.ApplyOrbit();
        return camera;
    }

    private static Grid ConfigureGrid(Grid grid, bool enabled)
    {
        ArgumentNullException.ThrowIfNull(grid);

        grid.Enabled = enabled;
        grid.Spacing = 3.5f;
        grid.MajorStep = 12;
        grid.LineWidth = 0.9f;
        grid.EdgeLineWidthScale = 1.75f;
        grid.FadeEnabled = false;
        return grid;
    }

    private static void AddFallbackLight(Scene scene)
    {
        Light light = scene.RootNode.AddNode<Light>("KeyLight");
        light.Position = new Vector3(2.0f, 3.0f, 1.5f);
        light.Color = new Vector3(1.0f, 0.98f, 0.9f);
        light.Intensity = 1.0f;
    }

    private static void EnsurePreviewLights(Scene scene, SceneBounds bounds, int desiredCount)
    {
        Light[] existingLights = scene.GetDescendants<Light>();
        if (existingLights.Length > 0)
        {
            return;
        }

        float radius = MathF.Max(bounds.Radius, 1.0f);
        Vector3 center = bounds.Center;
        int lightCount = Math.Max(1, Math.Min(desiredCount, 3));
        float lightDistance = radius * 1.85f;

        Vector3[] directions =
        [
            Vector3.Normalize(new Vector3(0.9f, 1.25f, 0.35f)),
            Vector3.Normalize(new Vector3(-1.15f, 0.4f, -0.7f)),
            Vector3.Normalize(new Vector3(-0.2f, 0.95f, 1.15f))
        ];

        Vector3[] colors =
        [
            new Vector3(1.0f, 0.92f, 0.78f),
            new Vector3(0.55f, 0.66f, 0.95f),
            new Vector3(0.82f, 0.88f, 1.0f)
        ];

        float[] intensities = [0.72f, 0.2f, 0.34f];

        for (int i = 0; i < lightCount; i++)
        {
            Light light = scene.RootNode.AddNode<Light>($"PreviewLight_{i + 1}");
            light.Position = center + (directions[i] * lightDistance);
            light.Color = colors[i];
            light.Intensity = intensities[i];
            light.Enabled = true;
        }
    }

    private static DataBuffer<float> CreatePositions()
    {
        DataBuffer<float> positions = new(3, 1, 3);
        positions.Add(new Vector3(-0.9f, -0.7f, 0.0f));
        positions.Add(new Vector3(0.9f, -0.7f, 0.0f));
        positions.Add(new Vector3(0.0f, 0.85f, 0.0f));
        return positions;
    }

    private static DataBuffer<float> CreateNormals()
    {
        DataBuffer<float> normals = new(3, 1, 3);
        normals.Add(new Vector3(0.0f, 0.0f, 1.0f));
        normals.Add(new Vector3(0.0f, 0.0f, 1.0f));
        normals.Add(new Vector3(0.0f, 0.0f, 1.0f));
        return normals;
    }

    private static DataBuffer<uint> CreateIndices()
    {
        DataBuffer<uint> indices = new(3, 1, 1);
        indices.Add(0u);
        indices.Add(1u);
        indices.Add(2u);
        return indices;
    }
}
