using RedFox.Graphics3D;
using RedFox.Graphics3D.Rendering;
using RedFox.Graphics3D.Rendering.Hosting;
using RedFox.Graphics3D.Silk;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using System.Numerics;

namespace RedFox.Samples.Examples;

internal static class SilkMeshSampleRunner
{
    public static int Run(string[] arguments, string title, string fallbackSceneName, ISilkRendererBackendFactory backendFactory)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(fallbackSceneName);
        ArgumentNullException.ThrowIfNull(backendFactory);

        if (!MeshSampleSceneFactory.TryCreate(arguments, fallbackSceneName, out MeshSampleSceneContext? context, out string? error))
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        ArgumentNullException.ThrowIfNull(context);
        Scene scene = context.Scene;
        Grid? grid = context.Grid;
        SceneViewportController viewportController = context.ViewportController;
        MeshSampleOptions options = context.Options;

        if (context.AnimationPlayers.Count > 0)
        {
            Console.WriteLine($"[Animation] Created {context.AnimationPlayers.Count} skeletal animation player(s).");
        }

        SilkCameraInputAdapter? cameraInputAdapter = null;
        bool inputInitialized = false;
        Vector4 clearColor = new(0.07f, 0.09f, 0.13f, 1.0f);
        Vector3 ambientColor = new(0.13f, 0.13f, 0.16f);
        Vector3 fallbackLightDirection = Vector3.Normalize(new Vector3(-0.4f, -1.0f, -0.2f));
        Vector3 fallbackLightColor = Vector3.One;
        float fallbackLightIntensity = 0.8f;

        WindowOptions windowOptions = WindowOptions.Default;
        windowOptions.Title = title;
        windowOptions.Size = new Vector2D<int>(1280, 720);
        windowOptions.PreferredDepthBufferBits = 24;
        windowOptions.PreferredStencilBufferBits = 8;
        using SilkRendererHost host = new(
            windowOptions,
            backendFactory,
            graphicsDevice => new SceneRenderer(
                graphicsDevice,
                clearColor,
                ambientColor,
                fallbackLightDirection,
                fallbackLightColor,
                fallbackLightIntensity,
                options.UseViewBasedLighting,
                options.SkinningMode));
        host.Window.FramebufferResize += size =>
        {
            if (size.X > 0 && size.Y > 0)
            {
                viewportController.ResizeViewport(size.X, size.Y);
            }
        };

        float elapsedSeconds = 0.0f;
        host.Run((deltaTime, input, renderer) =>
        {
            if (!inputInitialized)
            {
                cameraInputAdapter = new SilkCameraInputAdapter(input)
                {
                    LookSensitivity = 0.0052f,
                    ZoomSensitivity = 0.25f,
                    PanSensitivity = 0.0022f,
                    DollySensitivity = 0.014f,
                    RequireAltForMouseGestures = true,
                };
                if (input.Keyboards.Count > 0)
                {
                    input.Keyboards[0].KeyDown += (_, key, _) => OnKeyDown(key, renderer, scene, viewportController, grid);
                }

                inputInitialized = true;
            }

            if (cameraInputAdapter is not null)
            {
                viewportController.UpdateAndRender(renderer, cameraInputAdapter, (float)deltaTime);

                elapsedSeconds += (float)deltaTime;
                if (options.ExitAfterSeconds > 0.0f && elapsedSeconds >= options.ExitAfterSeconds)
                {
                    host.Window.Close();
                }
            }
        });
        return 0;
    }

    private static void OnKeyDown(
        Key key,
        SceneRenderer renderer,
        Scene scene,
        SceneViewportController viewportController,
        Grid? grid)
    {
        switch (key)
        {
            case Key.L:
                renderer.UseViewBasedLighting = !renderer.UseViewBasedLighting;
                Console.WriteLine($"[Lighting] View-based lighting {(renderer.UseViewBasedLighting ? "enabled" : "disabled")}");
                break;

            case Key.B:
                bool showSkeletonBones = ToggleSkeletonBones(scene);
                viewportController.RecomputeBounds();
                Console.WriteLine($"[Skeleton] Bones {(showSkeletonBones ? "visible" : "hidden")}");
                break;

            case Key.F:
                if (viewportController.RecomputeBounds() && viewportController.FitCameraToScene())
                {
                    Console.WriteLine("[Camera] Fit to scene.");
                }

                break;

            case Key.K:
                renderer.SkinningMode = renderer.SkinningMode == SkinningMode.Linear
                    ? SkinningMode.DualQuaternion
                    : SkinningMode.Linear;
                Console.WriteLine($"[Skinning] Mode: {renderer.SkinningMode}");
                break;

            case Key.Space:
                scene.IsAnimationPaused = !scene.IsAnimationPaused;
                Console.WriteLine($"[Animation] {(scene.IsAnimationPaused ? "Paused" : "Playing")}");
                break;

            case Key.T:
                bool enable = scene.IsAnimationPaused;
                scene.IsAnimationPaused = !enable;
                if (!enable)
                {
                    foreach (SceneNode node in scene.EnumerateDescendants())
                    {
                        node.ResetLiveTransform();
                    }

                    Console.WriteLine("[Animation] Disabled; bind pose restored.");
                }
                else
                {
                    Console.WriteLine("[Animation] Enabled.");
                }

                break;

            case Key.G:
                if (grid is not null)
                {
                    grid.Enabled = !grid.Enabled;
                    Console.WriteLine($"[Grid] {(grid.Enabled ? "Visible" : "Hidden")}");
                }

                break;
        }
    }

    private static bool ToggleSkeletonBones(Scene scene)
    {
        SkeletonBone[] bones = scene.GetDescendants<SkeletonBone>();
        bool showSkeletonBones = bones.Any(bone => !bone.ShowSkeletonBone);
        for (int i = 0; i < bones.Length; i++)
        {
            bones[i].ShowSkeletonBone = showSkeletonBones;
        }

        return showSkeletonBones;
    }
}
