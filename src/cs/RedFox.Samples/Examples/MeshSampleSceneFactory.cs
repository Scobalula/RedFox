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
        ApplySkeletonVisibility(scene, options.ShowSkeletonBones);

        IReadOnlyList<AnimationPlayer> animationPlayers = scene.CreateAnimationPlayers();
        OrbitCamera camera = scene.RootNode.AddNode(CreateCamera());
        SceneViewportController viewportController = new(scene, camera)
        {
            RefreshAnimatedSceneBounds = animationPlayers.Count > 0,
            IncludeNodeInBounds = ShouldIncludeInBounds,
        };

        SceneBounds? previewBounds = null;
        if (viewportController.RecomputeBounds())
        {
            viewportController.FitCameraToScene();
            EnsurePreviewLights(scene, viewportController.Bounds, PreviewRandomLightCount);
            previewBounds = GetAxisAdjustedBounds(viewportController.Bounds, scene.UpAxis);
        }
        else
        {
            AddFallbackLight(scene);
        }

        Grid grid = ConfigureGrid(scene.Grid, options.ShowGrid, previewBounds);
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

            if (arg.Equals("--show-skeleton", StringComparison.OrdinalIgnoreCase)
                || arg.Equals("--show-bones", StringComparison.OrdinalIgnoreCase))
            {
                options.ShowSkeletonBones = true;
                continue;
            }

            if (arg.Equals("--hide-skeleton", StringComparison.OrdinalIgnoreCase)
                || arg.Equals("--hide-bones", StringComparison.OrdinalIgnoreCase))
            {
                options.ShowSkeletonBones = false;
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

            if (arg.Equals("--frame-stats", StringComparison.OrdinalIgnoreCase)
                || arg.Equals("--stats", StringComparison.OrdinalIgnoreCase))
            {
                options.FrameStats = true;
                continue;
            }

            if (arg.Equals("--vsync", StringComparison.OrdinalIgnoreCase))
            {
                options.VSync = true;
                continue;
            }

            if (arg.Equals("--no-vsync", StringComparison.OrdinalIgnoreCase))
            {
                options.VSync = false;
                continue;
            }

            if (arg.Equals("--no-aa", StringComparison.OrdinalIgnoreCase)
                || arg.Equals("--no-antialiasing", StringComparison.OrdinalIgnoreCase))
            {
                options.AntiAliasingSamples = 1;
                continue;
            }

            if (arg.StartsWith("--aa=", StringComparison.OrdinalIgnoreCase)
                || arg.StartsWith("--anti-aliasing=", StringComparison.OrdinalIgnoreCase))
            {
                int valueStart = arg.IndexOf('=', StringComparison.Ordinal) + 1;
                if (int.TryParse(arg[valueStart..].Trim(), out int sampleCount))
                {
                    options.AntiAliasingSamples = Math.Max(1, sampleCount);
                }

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

    private static void ApplySkeletonVisibility(Scene scene, bool showSkeletonBones)
    {
        SkeletonBone[] bones = scene.GetDescendants<SkeletonBone>();
        for (int i = 0; i < bones.Length; i++)
        {
            bones[i].ShowSkeletonBone = showSkeletonBones;
        }
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
        SampleImageTranslatorRegistry.RegisterDefaults(scene.ImageTranslators);

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
                sceneTranslatorOptions.Set(ObjTranslator.MergeStaticMeshesOption, true);
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

    internal static SceneTranslatorManager CreateTranslatorManager()
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
        SampleImageTranslatorRegistry.RegisterDefaults(scene.ImageTranslators);
        Mesh mesh = scene.RootNode.AddNode<Mesh>("Triangle");
        mesh.Positions = CreatePositions();
        mesh.Normals = CreateNormals();
        mesh.FaceIndices = CreateIndices();

        Material material = new("TriangleMaterial")
        {
            DiffuseColor = new Vector4(0.92f, 0.3f, 0.24f, 1.0f)
        };

        mesh.Materials = [material];
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

    private static Grid ConfigureGrid(Grid grid, bool enabled, SceneBounds? bounds)
    {
        ArgumentNullException.ThrowIfNull(grid);

        if (bounds is { IsValid: true } sceneBounds)
        {
            grid.ConfigureForBounds(sceneBounds);
        }
        else
        {
            grid.Spacing = 1.0f;
            grid.MajorStep = 10;
            grid.LineWidth = 1.1f;
            grid.EdgeLineWidthScale = 1.2f;
            grid.MinimumPixelsBetweenCells = 2.5f;
        }

        grid.Enabled = enabled;
        return grid;
    }

    private static SceneBounds GetAxisAdjustedBounds(SceneBounds bounds, SceneUpAxis upAxis)
    {
        Matrix4x4 sceneAxisMatrix = GetSceneAxisMatrix(upAxis);
        if (sceneAxisMatrix == Matrix4x4.Identity)
        {
            return bounds;
        }

        Vector3[] corners = GetBoundsCorners(bounds);
        Vector3 transformedMin = new(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 transformedMax = new(float.MinValue, float.MinValue, float.MinValue);

        for (int cornerIndex = 0; cornerIndex < corners.Length; cornerIndex++)
        {
            Vector3 transformed = Vector3.Transform(corners[cornerIndex], sceneAxisMatrix);
            transformedMin = Vector3.Min(transformedMin, transformed);
            transformedMax = Vector3.Max(transformedMax, transformed);
        }

        return new SceneBounds(transformedMin, transformedMax);
    }

    private static Vector3[] GetBoundsCorners(SceneBounds bounds)
    {
        Vector3 min = bounds.Min;
        Vector3 max = bounds.Max;

        return
        [
            new Vector3(min.X, min.Y, min.Z),
            new Vector3(max.X, min.Y, min.Z),
            new Vector3(min.X, max.Y, min.Z),
            new Vector3(max.X, max.Y, min.Z),
            new Vector3(min.X, min.Y, max.Z),
            new Vector3(max.X, min.Y, max.Z),
            new Vector3(min.X, max.Y, max.Z),
            new Vector3(max.X, max.Y, max.Z)
        ];
    }

    private static Matrix4x4 GetSceneAxisMatrix(SceneUpAxis upAxis)
    {
        return upAxis switch
        {
            SceneUpAxis.X => Matrix4x4.CreateRotationZ(MathF.PI * 0.5f),
            SceneUpAxis.Z => Matrix4x4.CreateRotationX(-MathF.PI * 0.5f),
            _ => Matrix4x4.Identity,
        };
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
