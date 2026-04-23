# Renderer Refactor — Layered Rendering Architecture

This document describes the refactor that split the monolithic `RedFox.Graphics3D.OpenGL`
project into a layered rendering stack: a backend-agnostic abstraction layer, a slimmed-down
low-level OpenGL resource layer, and a high-level OpenGL renderer that owns scene-aware
passes, handles, and a minimal windowed host.

## Project graph

```
RedFox.Graphics3D
        │
        ├── RedFox.Rendering                  (abstract pipeline + pass interfaces)
        │           │
        │           └── RedFox.Rendering.OpenGL  (concrete passes, handles, renderer, host)
        │                       │
        └── RedFox.Graphics3D.OpenGL          (low-level GL resource wrappers only)
                                │
                                └── (consumed by RedFox.Rendering.OpenGL)
```

Three projects, sharply layered:

| Project | Purpose | References |
| --- | --- | --- |
| `RedFox.Rendering` | Backend-agnostic. Defines `SceneRenderer`, `IRenderPass`, `IRenderPipeline`, `RenderPipeline`, `RenderPassPhase`, `RenderFrameContext`, and built-in pass marker interfaces. | `RedFox.Graphics3D` |
| `RedFox.Graphics3D.OpenGL` | Low-level GL primitives only — `OpenGlContext`, `GlProgramBase`, `GlShaderProgram`, `GlComputeProgram`. Knows nothing about scenes. | `RedFox.Graphics3D`, `Silk.NET.OpenGL` |
| `RedFox.Rendering.OpenGL` | Scene-aware OpenGL backend: settings, shaders, frame queues, scene-node handles, render passes, `OpenGlSceneRenderer` orchestrator, minimal `OpenGlRendererHost`. | `RedFox.Rendering`, `RedFox.Graphics3D.OpenGL`, `Silk.NET.{OpenGL, Windowing, Input}` |

## Pass phases

`RenderPassPhase` defines a deterministic execution order. Passes added to an
`IRenderPipeline` are sorted stably by phase, with insertion order as the tiebreaker.

| Phase value | Phase | Built-in OpenGL pass |
| ---: | --- | --- |
| 0 | `Setup` | `OpenGlClearAndStateResetPass` |
| 100 | `Collect` | `OpenGlSceneCollectionPass` |
| 200 | `Compute` | `OpenGlSkinningComputePass` |
| 300 | `Shadow` | *(extension point)* |
| 400 | `Opaque` | `OpenGlOpaqueGeometryPass` |
| 500 | `Transparent` | `OpenGlTransparentGeometryPass` |
| 600 | `Overlay` | `OpenGlSkeletonOverlayPass` |
| 700 | `PostProcess` | *(extension point)* |
| 800 | `Present` | *(extension point)* |

`RedFox.Rendering.Passes` exposes marker interfaces for each phase
(`ISceneCollectionPass`, `ISkinningPass`, `IShadowPass`, `IOpaqueGeometryPass`,
`ITransparentGeometryPass`, `IOverlayPass`, `IPostProcessPass`). A backend's concrete passes
implement the appropriate marker so callers can find or replace them by capability rather
than by concrete type.

## Render frame context

Each frame the orchestrator builds a `RenderFrameContext` carrying:

- `Scene Scene`
- `in CameraView View`
- `Vector2 ViewportSize`
- `float DeltaTime`
- A typed services bag (`Set<TService>`, `GetRequired<TService>`, `TryGet<TService>(out TService?)`)

Passes communicate through the bag. For example, the OpenGL collection pass walks
the scene graph and publishes an `OpenGlFrameQueues` instance that downstream passes
(skinning/opaque/transparent/overlay) consume.

## OpenGL handles

`IOpenGlSceneNodeRenderHandle` was removed. Each scene-node category has a concrete handle
class implementing only the small `ISceneNodeRenderHandle` lifecycle interface
(`Update`/`Release`/`Dispose`):

- `OpenGlMeshHandle` — owns VAO/VBO/IBO + skinning SSBO; `DrawOpaque(...)` and
  `DispatchSkinning(...)`.
- `OpenGlGridHandle` — line geometry; `DrawTransparent(...)`.
- `OpenGlSkeletonBoneHandle` — per-bone axis lines; `DrawOverlay(...)`.
- `OpenGlLightHandle` — data-only snapshot consumed by the collection pass.

Pass classes pattern-match on the concrete type and dispatch directly. There is no shared
backend interface beyond `ISceneNodeRenderHandle`.

## Orchestrator

`OpenGlSceneRenderer : SceneRenderer` is intentionally thin:

1. `Initialize` boots the GL context (`OpenGlContext.RequireVersion(4, 3)`),
   creates shared resources (`OpenGlRenderResources`), then populates a default
   `RenderPipeline` with the six built-in passes.
2. `Resize(width, height)` updates the viewport and forwards to the pipeline.
3. `Render(scene, in view, deltaTime)` builds a fresh `RenderFrameContext` and
   calls `pipeline.Execute(...)`.
4. `Pipeline` is exposed as `IRenderPipeline` so callers can append, remove, or
   reorder passes — e.g. inserting a custom shadow or post-process pass.

## Minimal host

`OpenGlRendererHost` owns the window, the renderer, and the input context — and nothing
else. Scenes and cameras live with the caller. Two `Run` overloads:

```csharp
host.Run();                                                  // empty render loop
host.Run((deltaTime, inputContext, renderer) => { /* … */ }); // app drives the frame
```

Inside the callback the caller polls input, updates scene/camera state, and invokes
`renderer.Render(scene, view, (float)deltaTime)`. The host subscribes
`FramebufferResize → renderer.Resize`; sample code subscribes to `host.Window.FramebufferResize`
to update camera aspect ratio.

## Sample migration

`RedFox.Samples/Examples/OpenGlMeshSample.cs` was updated to:

- Construct `OpenGlRendererHost(title, width, height, settings)`.
- Subscribe to `host.Window.FramebufferResize` for camera aspect updates.
- Drive the per-frame loop via `host.Run((dt, inputContext, renderer) => …)`,
  including scene update and explicit `renderer.Render(...)` invocation.

All keyboard toggles (`L`, `B`, `F`, `K`, `Space`, `T`, `G`) behave identically.

## Verification

- `dotnet build src/cs/RedFox.sln` — succeeds for all renderer-related projects.
  (A pre-existing unrelated `RedFox.Tests` failure in
  `Graphics3D/GltfTranslatorTests.cs` is out of scope for this refactor.)
- Manual smoke: the OpenGL mesh sample renders the fallback triangle, loads scene files,
  animates skeletons, and respects all input toggles.

## Out of scope

- Implementing SSAO, shadow-mapping, or post-process passes (interfaces only).
- Touching shaders beyond extracting them to `Shaders/BasicShaders.cs`.
- Non-OpenGL Graphics3D code beyond the `SceneRenderer` base move.
