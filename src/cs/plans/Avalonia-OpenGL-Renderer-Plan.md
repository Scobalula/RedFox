# Plan: Avalonia OpenGL Renderer

Avalonia hosting should reuse the renderer work already split out of the Silk sample path: `SceneRenderer` owns scene rendering, `IGraphicsDevice` owns backend resources, and host code owns platform/window/input lifetime. The Avalonia layer should be a thin control adapter, not a second renderer.

## Goals

- Use the latest stable Avalonia 12 packages consistently across the Avalonia renderer and sample app.
- Host the existing OpenGL backend inside an Avalonia control without depending on Silk windowing.
- Keep sample/app glue minimal: create device, create `SceneRenderer`, forward resize/input/frame ticks, dispose in control lifetime.
- Preserve the current `SceneRenderer` and material/render-handle behavior for Default, Grid, Skeleton, and Skinning.
- Make camera/input reusable between Silk samples and Avalonia controls where practical.
- Support opening supported scene/animation files from the UI and replacing either the full scene or selected scene nodes without recreating the whole renderer/backend.
- Expose renderer and scene controls through normal Avalonia bindings so app UI can toggle lighting, skinning mode, animation, overlays, and selection state without reaching through private host internals.
- Avoid settings DTOs unless a group of values has real behavior or invariants.

## Design

- **Avalonia 12 baseline**: create the Avalonia-specific project and sample on latest stable Avalonia 12. Keep package versions centralized so the control, sample app, themes, and desktop host cannot drift.
- **Control owns context lifetime**: create an Avalonia OpenGL control that obtains the platform `GlInterface`, initializes GL once, creates `OpenGlGraphicsDevice`, and disposes device/renderer when detached.
- **Renderer creation stays injected**: the control accepts a `Func<IGraphicsDevice, SceneRenderer>` or an equivalent minimal factory so apps can choose clear color, lighting, and skinning mode without subclassing.
- **Renderer state is bindable**: expose control state as Avalonia properties where it affects rendering or host behavior: `Scene`, `Camera`, `ClearColor`, `UseViewBasedLighting`, `SkinningMode`, `ShowGrid`, `ShowSkeleton`, `IsAnimationPaused`, and `SelectedNode`. Property changes should update the existing `SceneRenderer`/scene model on the UI thread and request a render.
- **Scene update stays external**: the control exposes a render callback/event that receives elapsed time and the `SceneRenderer`, matching the Silk host model without importing Silk types.
- **File loading is app-owned**: add a sample app service/view-model that uses Avalonia 12 storage APIs to open supported model/animation files, calls the existing translator manager, and assigns the resulting scene or node through bindable properties.
- **Node replacement is explicit**: provide a small scene-editing helper for replacing the selected node, importing nodes under an existing parent, or replacing the entire scene. Replacement should dispose/release old GPU handles via the normal scene traversal/update path rather than tearing down the OpenGL device.
- **Viewport resize is explicit**: Avalonia size changes call `SceneRenderer.Resize(width, height)` and update the OpenGL viewport through the backend path.
- **Input is adapter-based**: add an Avalonia camera input adapter that feeds the existing camera controller concepts from pointer wheel/move/button and keyboard events.
- **Render scheduling is host-local**: use Avalonia render invalidation or a lightweight timer tied to the control's visual lifetime. The control should stop scheduling when detached or invisible.

## Steps

1. **Project boundary** — add a small Avalonia-specific project, likely `RedFox.Graphics3D.Avalonia`, referencing `RedFox.Graphics3D`, `RedFox.Graphics3D.OpenGL`, and latest stable Avalonia 12 packages. Put the Avalonia version in central package management or a shared MSBuild property.
2. **OpenGL context adapter** — implement an `AvaloniaOpenGlRendererControl` around Avalonia's OpenGL control/lifetime APIs. It should create `OpenGlGraphicsDevice` from the current GL context and avoid any Silk dependency.
3. **Bindable control surface** — define `StyledProperty`/`DirectProperty` members for scene/render state. Keep simple value properties directly on the control; do not introduce a renderer settings DTO.
4. **Renderer factory hook** — constructor or property accepts `Func<IGraphicsDevice, SceneRenderer>`. Provide a sensible default only if it does not hide app choices.
5. **Render callback hook** — expose an event/delegate for per-frame scene updates, equivalent to the Silk sample runner callback.
6. **File-open workflow** — in the Avalonia sample app, add `OpenFileCommand` using Avalonia 12 `IStorageProvider.OpenFilePickerAsync`. Reuse the existing translator registrations and support the same mesh/animation combinations as the Silk sample.
7. **Scene/node replacement workflow** — add a small `SceneEditService` or equivalent helper with operations for `ReplaceScene(Scene scene)`, `ReplaceNode(SceneNode target, SceneNode replacement)`, and `ImportUnder(SceneNode parent, SceneNode importedRoot)`. The sample binds selected tree items to `SelectedNode` and routes replace/import commands through this helper.
8. **Resize and framebuffer handling** — on control size/framebuffer changes, clamp to at least 1x1, call `SceneRenderer.Resize`, and ensure the OpenGL viewport/default target state matches the current framebuffer.
9. **Avalonia input adapter** — map Avalonia pointer/keyboard events into the shared camera controller. Keep this separate from the control so custom app input can bypass it.
10. **Sample integration** — add an Avalonia 12 desktop sample app or sample page that loads the same mesh/animation arguments as the Silk samples and uses the same scene construction helpers where possible.
11. **Ownership cleanup** — make sure `SceneRenderer`, `OpenGlGraphicsDevice`, old scene GPU handles, replaced nodes, and transient input subscriptions are disposed/unsubscribed when the control detaches or scene content is replaced.

## API Sketch

```csharp
public sealed class AvaloniaOpenGlRendererControl : OpenGlControlBase
{
    public static readonly StyledProperty<Scene?> SceneProperty;

    public static readonly StyledProperty<OrbitCamera?> CameraProperty;

    public static readonly StyledProperty<bool> UseViewBasedLightingProperty;

    public static readonly StyledProperty<SkinningMode> SkinningModeProperty;

    public static readonly StyledProperty<SceneNode?> SelectedNodeProperty;

    public Func<IGraphicsDevice, SceneRenderer>? RendererFactory { get; set; }

    public SceneRenderer? Renderer { get; }

    public event EventHandler<AvaloniaRenderFrameEventArgs>? RenderFrame;
}
```

```csharp
public sealed class AvaloniaRenderFrameEventArgs : EventArgs
{
    public required SceneRenderer Renderer { get; init; }

    public required Scene Scene { get; init; }

    public required TimeSpan ElapsedTime { get; init; }
}
```

The sample app should use normal MVVM bindings over those properties. File and node commands belong in the app view model, while the renderer control remains focused on rendering and input translation.

## MVVM Binding Contract

The target shape is that app code can treat the scene as ordinary view-model state. Assigning `ViewModel.Scene = new Scene(...)` updates the bound renderer control, keeps the OpenGL backend alive, releases old scene GPU handles, initializes GPU handles for the new scene on the next render/update pass, and requests a redraw.

```xml
<avalonia:AvaloniaOpenGlRendererControl
    Scene="{Binding Scene}"
    Camera="{Binding Camera}"
    SelectedNode="{Binding SelectedNode}"
    UseViewBasedLighting="{Binding UseViewBasedLighting}"
    SkinningMode="{Binding SkinningMode}" />
```

```csharp
public Scene? Scene
{
    get => _scene;
    set => SetProperty(ref _scene, value);
}
```

Adding and removing nodes should also be view-model friendly. The sample app can expose commands such as `AddNodeCommand`, `RemoveSelectedNodeCommand`, `ReplaceSelectedNodeCommand`, and `ClearSceneCommand`. Those commands mutate the bound `Scene` through scene graph APIs, then notify the renderer control by either property change, a scene graph version/change event, or a small explicit invalidation API. The important outcome is that UI code never reaches into `OpenGlGraphicsDevice` or renderer internals just to change scene content.

## Sample App Workflow

1. **Open file**: `OpenFileCommand` shows the Avalonia 12 file picker, loads one or more selected paths through `SceneTranslatorManager`, creates animation players, and assigns `RendererControl.Scene`.
2. **Open animation**: when a scene is already loaded, selected animation files can be read into the current scene and immediately recreate animation players without recreating the OpenGL backend.
3. **Replace scene**: assigning a new `Scene` releases old scene GPU handles through traversal/update cleanup and keeps the same `OpenGlGraphicsDevice` alive.
4. **Replace selected node**: `ReplaceSelectedNodeCommand` imports a file into a temporary scene, picks its root/imported node, replaces `SelectedNode`, and recomputes scene bounds/camera fitting.
5. **Import under selected node**: `ImportUnderSelectedNodeCommand` attaches imported root children under `SelectedNode` while preserving the existing scene root, camera, grid, and lights.
6. **Renderer controls**: checkboxes/combos bind to `UseViewBasedLighting`, `SkinningMode`, `IsAnimationPaused`, skeleton overlay visibility, grid visibility, and selected node state. Changes request a render but do not recreate the backend.

## Relevant Files

- [RedFox.Graphics3D/Rendering/SceneRenderer.cs](RedFox.Graphics3D/Rendering/SceneRenderer.cs) — reused directly by the control.
- [RedFox.Graphics3D/Rendering/Backend/IGraphicsDevice.cs](RedFox.Graphics3D/Rendering/Backend/IGraphicsDevice.cs) — backend boundary the control depends on.
- [RedFox.Graphics3D.OpenGL/Backend/OpenGlGraphicsDevice.cs](RedFox.Graphics3D.OpenGL/Backend/OpenGlGraphicsDevice.cs) — OpenGL backend to construct from Avalonia's active context.
- [RedFox.Graphics3D.Silk/SilkRendererHost.cs](RedFox.Graphics3D.Silk/SilkRendererHost.cs) — reference host lifecycle, not a dependency.
- [RedFox.Samples/Examples/SilkMeshSampleRunner.cs](RedFox.Samples/Examples/SilkMeshSampleRunner.cs) — scene/sample conventions to reuse in the Avalonia sample.
- `RedFox.Graphics3D.Avalonia` — new Avalonia 12 control project for the OpenGL renderer control and Avalonia input adapter.
- `RedFox.Samples.Avalonia` or equivalent — new Avalonia 12 sample app with file picker commands, scene tree binding, and renderer controls.

## Verification

1. `dotnet build RedFox.sln` succeeds.
2. Avalonia sample opens, renders the fallback triangle/mesh path, and responds to resize.
3. Avalonia sample loads the same SEModel/SEAnim command shape as the Silk mesh samples.
4. Avalonia sample opens files from the file picker, replaces the full scene, and imports/replaces selected nodes from the scene tree.
5. Renderer controls update through Avalonia bindings and do not require direct calls into private renderer/backend members.
6. Camera controls work through the Avalonia input adapter.
7. OpenGL skinning path still dispatches compute for skinned assets.
8. Detaching/closing the control releases GL resources without leaking render callbacks or timers.
9. Replacing scenes/nodes does not recreate the OpenGL backend and does not retain old GPU handles.

## Decisions

- Avalonia control is a host adapter only; it should not fork `SceneRenderer`.
- Target latest stable Avalonia 12 for the control and sample app.
- No Silk types in the Avalonia project.
- Renderer construction is injected with a small delegate instead of a settings object.
- Renderer/app state is controlled through Avalonia properties and commands, not by exposing backend internals.
- File loading and scene graph mutation are sample/app responsibilities; the renderer control consumes the resulting bound `Scene`.
- Input translation is separate from rendering so future app controls can own their own UX.
- The first milestone should be OpenGL-only; D3D11/Avalonia interop can be planned separately if needed.
