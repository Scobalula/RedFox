using RedFox.Graphics3D;
using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.Bvh;
using RedFox.Graphics3D.Cast;
using RedFox.Graphics3D.Gltf;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.KaydaraFbx;
using RedFox.Graphics3D.MayaAscii;
using RedFox.Graphics3D.Md5;
using RedFox.Graphics3D.SEAnim;
using RedFox.Graphics3D.Semodel;
using RedFox.Graphics3D.Smd;
using RedFox.Graphics3D.WavefrontObj;
using RedFox.Graphics3D.Rendering;
using RedFox.Graphics3D.Rendering.Hosting;
using RedFox.Rendering.OpenGL;
using RedFox.Rendering.OpenGL.Hosting;
using Silk.NET.Input;
using System;
using System.Globalization;
using System.Collections.Generic;
using System.Numerics;

namespace RedFox.Samples.Examples;

/// <summary>
/// Runs a minimal OpenGL mesh rendering demo using the RedFox scene graph.
/// </summary>
internal sealed class OpenGlMeshSample : ISample
{
    private const int PreviewRandomLightCount = 3;

    /// <inheritdoc />
    public string Name => "graphics3d-opengl-mesh";

    /// <inheritdoc />
    public string Description => "Opens a window and renders a lit untextured mesh scene from one or more supported translator files (or a fallback triangle).";

    /// <inheritdoc />
    public int Run(string[] arguments)
    {
        ParseOptions(arguments, out List<string> scenePaths, out bool showGrid, out SceneUpAxis upAxis, out FaceWinding faceWinding, out bool useViewBasedLighting, out SkinningMode skinningMode, out float exitAfterSeconds);

        if (!TryCreateScene(scenePaths, out Scene? scene, out string? error))
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        ArgumentNullException.ThrowIfNull(scene);
        scene.UpAxis = upAxis;
        scene.FaceWinding = faceWinding;

        SkeletonOverlay skeletonOverlay = scene.TryGetFirstOfType<SkeletonOverlay>(out SkeletonOverlay? existingOverlay) && existingOverlay is not null
            ? existingOverlay
            : scene.RootNode.AddNode<SkeletonOverlay>("SkeletonOverlay");

        IReadOnlyList<AnimationPlayer> animationPlayers = scene.CreateAnimationPlayers();
        if (animationPlayers.Count > 0)
        {
            Console.WriteLine($"[Animation] Created {animationPlayers.Count} skeletal animation player(s).");
        }

        OrbitCamera camera = scene.RootNode.AddNode<OrbitCamera>("MainCamera");
        camera.AspectRatio = 1280.0f / 720.0f;
        camera.NearPlane = 0.01f;
        camera.FarPlane = 5000.0f;
        camera.FieldOfView = 60.0f;
        camera.LookSensitivity = 1.0f;
        camera.ZoomSensitivity = 1.0f;
        camera.PanSensitivity = 1.0f;
        camera.MoveSpeed = 2.5f;
        camera.BoostMultiplier = 3.0f;
        camera.MinDistance = 0.05f;
        camera.MaxDistance = 1000000.0f;
        camera.UsePitchLimits = false;
        camera.InvertX = true;
        camera.InvertY = true;
        camera.ApplyOrbit();

        SceneViewportController viewportController = new(scene, camera)
        {
            RefreshAnimatedSceneBounds = animationPlayers.Count > 0,
            IncludeNodeInBounds = node => ShouldIncludeInBounds(node, skeletonOverlay.ShowSkeletonBones),
        };

        if (viewportController.RecomputeBounds())
        {
            viewportController.FitCameraToScene();
            EnsurePreviewLights(scene, viewportController.Bounds, PreviewRandomLightCount);
        }
        else
        {
            Light light = scene.RootNode.AddNode<Light>("KeyLight");
            light.Position = new Vector3(2.0f, 3.0f, 1.5f);
            light.Color = new Vector3(1.0f, 0.98f, 0.9f);
            light.Intensity = 1.0f;
        }

        Grid? grid = null;
        if (showGrid)
        {
            grid = scene.RootNode.AddNode<Grid>("Grid");
            grid.Spacing = 3.5f;
            grid.MajorStep = 12;
            grid.LineWidth = 0.9f;
            grid.EdgeLineWidthScale = 1.75f;
            grid.FadeEnabled = false;
        }

        SilkCameraInputAdapter? cameraInputAdapter = null;
        IKeyboard? keyboard = null;
        bool prevLKeyDown = false;
        bool prevBKeyDown = false;
        bool prevFKeyDown = false;
        bool prevKKeyDown = false;
        bool prevSpaceKeyDown = false;
        bool prevTKeyDown = false;
        bool prevGKeyDown = false;
        float elapsedSeconds = 0.0f;

        using OpenGlRendererHost host = new(
            "RedFox OpenGL Mesh Sample",
            1280,
            720,
            new Vector4(0.07f, 0.09f, 0.13f, 1.0f),
            new Vector3(0.13f, 0.13f, 0.16f),
            new Vector3(-0.4f, -1.0f, -0.2f),
            new Vector3(1.0f, 1.0f, 1.0f),
            0.8f,
            useViewBasedLighting,
            skinningMode);
        host.Window.FramebufferResize += size =>
        {
            if (size.X > 0 && size.Y > 0)
            {
                viewportController.ResizeViewport(size.X, size.Y);
            }
        };
        try
        {
            host.Run((deltaTime, inputContext, renderer) =>
            {
                cameraInputAdapter ??= new SilkCameraInputAdapter(inputContext)
                {
                    LookSensitivity = 0.0052f,
                    ZoomSensitivity = 0.25f,
                    PanSensitivity = 0.0022f,
                    DollySensitivity = 0.014f,
                    RequireAltForMouseGestures = true
                };

                keyboard ??= inputContext.Keyboards.Count > 0 ? inputContext.Keyboards[0] : null;

                bool lKeyDown = keyboard?.IsKeyPressed(Key.L) ?? false;
                if (lKeyDown && !prevLKeyDown)
                {
                    renderer.UseViewBasedLighting = !renderer.UseViewBasedLighting;
                    Console.WriteLine($"[Toggle] UseViewBasedLighting: {renderer.UseViewBasedLighting}");
                }

                bool bKeyDown = keyboard?.IsKeyPressed(Key.B) ?? false;
                if (bKeyDown && !prevBKeyDown)
                {
                    skeletonOverlay.ShowSkeletonBones = !skeletonOverlay.ShowSkeletonBones;
                    viewportController.RecomputeBounds();
                    Console.WriteLine($"[Toggle] ShowSkeletonBones: {skeletonOverlay.ShowSkeletonBones}");
                }

                bool fKeyDown = keyboard?.IsKeyPressed(Key.F) ?? false;
                if (fKeyDown && !prevFKeyDown)
                {
                    if (viewportController.RecomputeBounds() && viewportController.FitCameraToScene())
                    {
                        Console.WriteLine("[Toggle] Camera re-fit to scene bounds.");
                    }
                }

                bool kKeyDown = keyboard?.IsKeyPressed(Key.K) ?? false;
                if (kKeyDown && !prevKKeyDown)
                {
                    renderer.SkinningMode = renderer.SkinningMode == SkinningMode.Linear
                        ? SkinningMode.DualQuaternion
                        : SkinningMode.Linear;
                    Console.WriteLine($"[Toggle] SkinningMode: {renderer.SkinningMode}");
                }

                bool spaceKeyDown = keyboard?.IsKeyPressed(Key.Space) ?? false;
                if (spaceKeyDown && !prevSpaceKeyDown)
                {
                    scene.IsAnimationPaused = !scene.IsAnimationPaused;
                    Console.WriteLine($"[Toggle] AnimationPaused: {scene.IsAnimationPaused}");
                }

                bool tKeyDown = keyboard?.IsKeyPressed(Key.T) ?? false;
                if (tKeyDown && !prevTKeyDown)
                {
                    bool enable = scene.IsAnimationPaused;
                    scene.IsAnimationPaused = !enable;
                    if (!enable)
                    {
                        foreach (SceneNode node in scene.EnumerateDescendants())
                        {
                            node.ResetLiveTransform();
                        }
                        Console.WriteLine("[Toggle] Animations disabled — live transforms reset to bind pose.");
                    }
                    else
                    {
                        Console.WriteLine("[Toggle] Animations enabled.");
                    }
                }

                bool gKeyDown = keyboard?.IsKeyPressed(Key.G) ?? false;
                if (gKeyDown && !prevGKeyDown)
                {
                    if (grid is not null)
                    {
                        bool hidden = grid.Flags.HasFlag(SceneNodeFlags.NoDraw);
                        grid.Flags = hidden
                            ? grid.Flags & ~SceneNodeFlags.NoDraw
                            : grid.Flags | SceneNodeFlags.NoDraw;
                        Console.WriteLine($"[Toggle] Grid: {(hidden ? "visible" : "hidden")}");
                    }
                }

                prevLKeyDown = lKeyDown;
                prevBKeyDown = bKeyDown;
                prevFKeyDown = fKeyDown;
                prevKKeyDown = kKeyDown;
                prevSpaceKeyDown = spaceKeyDown;
                prevTKeyDown = tKeyDown;
                prevGKeyDown = gKeyDown;

                viewportController.UpdateAndRender(renderer, cameraInputAdapter, (float)deltaTime);

                elapsedSeconds += (float)deltaTime;
                if (exitAfterSeconds > 0.0f && elapsedSeconds >= exitAfterSeconds)
                {
                    host.Window.Close();
                }
            });
        }
        finally
        {
            cameraInputAdapter?.Dispose();
        }

        return 0;
    }

    private static bool TryCreateScene(IReadOnlyList<string> scenePaths, out Scene? scene, out string? error)
    {
        if (scenePaths.Count == 0)
        {
            scene = CreateFallbackScene();
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

                SceneTranslatorOptions options = new();
                manager.Read(inputPath, scene, options);
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

    private static Scene CreateFallbackScene()
    {
        Scene scene = new("OpenGL Scene");

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

    private static void ParseOptions(
        string[] arguments,
        out List<string> scenePaths,
        out bool showGrid,
        out SceneUpAxis upAxis,
        out FaceWinding faceWinding,
        out bool useViewBasedLighting,
        out SkinningMode skinningMode,
        out float exitAfterSeconds)
    {
        scenePaths = [];
        showGrid = true;
        upAxis = SceneUpAxis.Y;
        faceWinding = FaceWinding.CounterClockwise;
        useViewBasedLighting = false;
        skinningMode = SkinningMode.Linear;
        exitAfterSeconds = 0.0f;

        for (int i = 0; i < arguments.Length; i++)
        {
            string arg = arguments[i];
            if (arg.Equals("--no-grid", StringComparison.OrdinalIgnoreCase))
            {
                showGrid = false;
                continue;
            }

            if (arg.Equals("--grid", StringComparison.OrdinalIgnoreCase))
            {
                showGrid = true;
                continue;
            }

            if (arg.StartsWith("--up-axis=", StringComparison.OrdinalIgnoreCase))
            {
                string value = arg[10..].Trim();
                if (value.Equals("x", StringComparison.OrdinalIgnoreCase))
                {
                    upAxis = SceneUpAxis.X;
                }
                else if (value.Equals("z", StringComparison.OrdinalIgnoreCase))
                {
                    upAxis = SceneUpAxis.Z;
                }
                else
                {
                    upAxis = SceneUpAxis.Y;
                }

                continue;
            }

            if (arg.StartsWith("--winding=", StringComparison.OrdinalIgnoreCase))
            {
                string value = arg[10..].Trim();
                faceWinding = value.Equals("cw", StringComparison.OrdinalIgnoreCase)
                    ? FaceWinding.Clockwise
                    : FaceWinding.CounterClockwise;
                continue;
            }

            if (arg.Equals("--view-lighting", StringComparison.OrdinalIgnoreCase)
                || arg.Equals("--view-lit", StringComparison.OrdinalIgnoreCase))
            {
                useViewBasedLighting = true;
                continue;
            }

            if (arg.Equals("--scene-lighting", StringComparison.OrdinalIgnoreCase)
                || arg.Equals("--no-view-lighting", StringComparison.OrdinalIgnoreCase))
            {
                useViewBasedLighting = false;
                continue;
            }

            if (arg.StartsWith("--skinning=", StringComparison.OrdinalIgnoreCase))
            {
                string value = arg[11..].Trim();
                skinningMode = value.Equals("dual", StringComparison.OrdinalIgnoreCase)
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
                    exitAfterSeconds = parsedSeconds;
                }

                continue;
            }

            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                scenePaths.Add(arg);
            }
        }
    }

    private static bool ShouldIncludeInBounds(SceneNode node, bool showSkeletonBones)
    {
        if (!showSkeletonBones && node is SkeletonBone)
        {
            return false;
        }

        return true;
    }

    private static Vector3 GetForwardFromAngles(float yawRadians, float pitchRadians)
    {
        Quaternion rotation = Quaternion.CreateFromYawPitchRoll(yawRadians, pitchRadians, 0.0f);
        Vector3 forward = Vector3.Transform(-Vector3.UnitZ, rotation);
        if (forward.LengthSquared() < 1e-8f)
        {
            return -Vector3.UnitZ;
        }

        return Vector3.Normalize(forward);
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
