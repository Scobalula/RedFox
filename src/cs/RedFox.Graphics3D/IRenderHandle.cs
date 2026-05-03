using RedFox.Graphics3D.Rendering;
using System;
using System.Numerics;

namespace RedFox.Graphics3D;

/// <summary>
/// Represents an API-agnostic render handle that owns or references render-time state for a scene asset.
/// </summary>
public interface IRenderHandle : IDisposable
{
    /// <summary>
    /// Gets the render phases in which this handle has work to submit for the current frame.
    /// </summary>
    RenderPhaseMask RenderPhases { get; }

    /// <summary>
    /// Gets a value indicating whether this handle needs its update method called for the current frame.
    /// </summary>
    bool RequiresPerFrameUpdate { get; }

    /// <summary>
    /// Gets the set of flags that describe the characteristics or behavior of the render handle.
    /// </summary>
    RenderHandleFlags Flags { get; }

    /// <summary>
    /// Updates lazy resources, uploads dynamic data, and refreshes GPU state.
    /// </summary>
    /// <param name="commandList">The command list used for the update.</param>
    void Update(ICommandList commandList);

    /// <summary>
    /// Renders the handle for the supplied phase.
    /// </summary>
    /// <param name="commandList">The active command list.</param>
    /// <param name="phase">The render phase to execute.</param>
    /// <param name="view">The active view matrix.</param>
    /// <param name="projection">The active projection matrix.</param>
    /// <param name="sceneAxis">The scene-axis transform matrix.</param>
    /// <param name="cameraPosition">The active camera position.</param>
    /// <param name="viewportSize">The active viewport size in pixels.</param>
    void Render(
        ICommandList commandList,
        RenderPhase phase,
        in Matrix4x4 view,
        in Matrix4x4 projection,
        in Matrix4x4 sceneAxis,
        Vector3 cameraPosition,
        Vector2 viewportSize);

    /// <summary>
    /// Releases GPU resources owned by the handle.
    /// </summary>
    void Release();
}