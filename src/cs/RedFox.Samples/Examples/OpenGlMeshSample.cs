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
using RedFox.Rendering.OpenGL;
using RedFox.Rendering.OpenGL.Hosting;
using Silk.NET.Input;
using System;
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
        ParseOptions(arguments, out List<string> scenePaths, out bool showGrid, out SceneUpAxis upAxis, out FaceWinding faceWinding, out bool useViewBasedLighting, out SkinningMode skinningMode);

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

        bool hasSceneBounds = SceneBoundsCalculator.TryCompute(scene, out SceneBounds bounds, node => ShouldIncludeInBounds(node, skeletonOverlay.ShowSkeletonBones));
        bool refreshAnimatedSceneBounds = animationPlayers.Count > 0;
        if (hasSceneBounds)
        {
            ApplyBoundsToCamera(bounds, camera, upAxis);
            EnsurePreviewLights(scene, bounds, PreviewRandomLightCount);
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
                camera.AspectRatio = (float)size.X / size.Y;
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
                    hasSceneBounds = SceneBoundsCalculator.TryCompute(scene, out bounds, node => ShouldIncludeInBounds(node, skeletonOverlay.ShowSkeletonBones));
                    Console.WriteLine($"[Toggle] ShowSkeletonBones: {skeletonOverlay.ShowSkeletonBones}");
                }

                if (refreshAnimatedSceneBounds)
                {
                    hasSceneBounds = SceneBoundsCalculator.TryCompute(scene, out bounds, node => ShouldIncludeInBounds(node, skeletonOverlay.ShowSkeletonBones));
                }

                bool fKeyDown = keyboard?.IsKeyPressed(Key.F) ?? false;
                if (fKeyDown && !prevFKeyDown)
                {
                    hasSceneBounds = SceneBoundsCalculator.TryCompute(scene, out bounds, node => ShouldIncludeInBounds(node, skeletonOverlay.ShowSkeletonBones));
                    if (hasSceneBounds)
                    {
                        ApplyBoundsToCamera(bounds, camera, upAxis);
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

                CameraControllerInput cameraInput = cameraInputAdapter.ReadInput();
                camera.UpdateInput((float)deltaTime, cameraInput);

                if (hasSceneBounds)
                {
                    UpdateDynamicClipPlanes(GetAxisAdjustedBounds(bounds, upAxis), camera);
                }

                scene.Update((float)deltaTime);
                CameraView view = camera.GetView();
                renderer.Render(scene, view, (float)deltaTime);
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
        out SkinningMode skinningMode)
    {
        scenePaths = [];
        showGrid = true;
        upAxis = SceneUpAxis.Y;
        faceWinding = FaceWinding.CounterClockwise;
        useViewBasedLighting = false;
        skinningMode = SkinningMode.Linear;

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

            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                scenePaths.Add(arg);
            }
        }
    }

    private static void ApplyBoundsToCamera(SceneBounds bounds, OrbitCamera camera, SceneUpAxis upAxis)
    {
        SceneBounds adjustedBounds = GetAxisAdjustedBounds(bounds, upAxis);
        float radius = MathF.Max(adjustedBounds.Radius, 0.5f);
        float diagonal = MathF.Max(adjustedBounds.DiagonalLength, 1.0f);

        // Asset front in this sample pipeline is treated as +X.
        Vector3 preferredForward = Vector3.Normalize(new Vector3(1.0f, -0.28f, -0.2f));
        (float yaw, float pitch) = GetYawPitchFromForward(preferredForward);

        camera.OrbitTarget = adjustedBounds.Center;
        camera.YawRadians = yaw;
        camera.PitchRadians = pitch;

        camera.MinDistance = MathF.Max(radius * 0.0005f, 0.001f);
        camera.MaxDistance = MathF.Max(diagonal * 24.0f, 2000.0f);
        camera.MoveSpeed = MathF.Max(diagonal * 0.1f, 1.75f);
        camera.BoostMultiplier = 2.5f;
        camera.ZoomSensitivity = 1.0f;

        float aspect = camera.AspectRatio > 0.0f ? camera.AspectRatio : (16.0f / 9.0f);
        float fitDistance = ComputeBoundsFitDistance(adjustedBounds, preferredForward, camera.FieldOfView, aspect) * 1.15f;
        camera.Distance = Math.Clamp(fitDistance, camera.MinDistance, camera.MaxDistance);
        camera.ApplyOrbit();

        UpdateDynamicClipPlanes(adjustedBounds, camera);
    }

    private static bool ShouldIncludeInBounds(SceneNode node, bool showSkeletonBones)
    {
        if (!showSkeletonBones && node is SkeletonBone)
        {
            return false;
        }

        return true;
    }

    private static void UpdateDynamicClipPlanes(SceneBounds bounds, OrbitCamera camera)
    {
        float radius = MathF.Max(bounds.Radius, 1.0f);
        float distanceToCenter = Vector3.Distance(camera.Position, bounds.Center);

        // Keep clipping conservative and stable while avoiding runaway near-plane growth.
        float nearPlane = 0.01f;
        float farPlane = MathF.Max(distanceToCenter + (radius * 8.0f), MathF.Max(radius * 24.0f, 5000.0f));

        if (farPlane > 500000.0f)
        {
            nearPlane = MathF.Max(nearPlane, farPlane / 1000000.0f);
        }

        camera.NearPlane = nearPlane;
        camera.FarPlane = farPlane;
    }

    private static float ComputeSphereFitDistance(float radius, float verticalFovDegrees, float aspectRatio)
    {
        float verticalFov = MathF.Max(verticalFovDegrees * (MathF.PI / 180.0f), 1e-3f);
        float halfVertical = verticalFov * 0.5f;
        float horizontalFov = 2.0f * MathF.Atan(MathF.Tan(halfVertical) * MathF.Max(aspectRatio, 1e-3f));
        float limitingHalfFov = MathF.Min(halfVertical, horizontalFov * 0.5f);
        float sinHalfFov = MathF.Max(MathF.Sin(limitingHalfFov), 1e-4f);
        return MathF.Max(radius / sinHalfFov, radius * 1.1f);
    }

    private static float ComputeBoundsFitDistance(SceneBounds bounds, Vector3 forward, float verticalFovDegrees, float aspectRatio)
    {
        Vector3 normalizedForward = forward.LengthSquared() < 1e-8f ? -Vector3.UnitZ : Vector3.Normalize(forward);
        Vector3 upHint = MathF.Abs(Vector3.Dot(normalizedForward, Vector3.UnitY)) > 0.98f
            ? Vector3.UnitZ
            : Vector3.UnitY;

        Vector3 right = Vector3.Cross(normalizedForward, upHint);
        if (right.LengthSquared() < 1e-8f)
        {
            right = Vector3.Cross(normalizedForward, Vector3.UnitX);
        }

        right = Vector3.Normalize(right);
        Vector3 up = Vector3.Normalize(Vector3.Cross(right, normalizedForward));

        float verticalFov = MathF.Max(verticalFovDegrees * (MathF.PI / 180.0f), 1e-3f);
        float halfVertical = verticalFov * 0.5f;
        float halfHorizontal = MathF.Atan(MathF.Tan(halfVertical) * MathF.Max(aspectRatio, 1e-3f));
        float tanVertical = MathF.Max(MathF.Tan(halfVertical), 1e-4f);
        float tanHorizontal = MathF.Max(MathF.Tan(halfHorizontal), 1e-4f);

        Vector3[] corners = GetBoundsCorners(bounds);
        Vector3 center = bounds.Center;
        float requiredDistance = 0.0f;

        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 relative = corners[i] - center;
            float x = MathF.Abs(Vector3.Dot(relative, right));
            float y = MathF.Abs(Vector3.Dot(relative, up));
            float z = Vector3.Dot(relative, normalizedForward);

            float distanceForX = (x / tanHorizontal) - z;
            float distanceForY = (y / tanVertical) - z;
            float cornerDistance = MathF.Max(distanceForX, distanceForY);
            requiredDistance = MathF.Max(requiredDistance, cornerDistance);
        }

        return MathF.Max(requiredDistance, bounds.Radius * 1.1f);
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

        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 transformed = Vector3.Transform(corners[i], sceneAxisMatrix);
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
            _ => Matrix4x4.Identity
        };
    }

    private static (float YawRadians, float PitchRadians) GetYawPitchFromForward(Vector3 forward)
    {
        Vector3 normalized = forward;
        if (normalized.LengthSquared() < 1e-8f)
        {
            normalized = -Vector3.UnitZ;
        }
        else
        {
            normalized = Vector3.Normalize(normalized);
        }

        float yaw = MathF.Atan2(normalized.X, -normalized.Z);
        float pitch = MathF.Asin(Math.Clamp(normalized.Y, -1.0f, 1.0f));
        return (yaw, pitch);
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
