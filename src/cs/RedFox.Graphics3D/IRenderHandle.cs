using RedFox.Graphics3D.Rendering;
using RedFox.Graphics3D.Rendering.Backend;
using System;
using System.Numerics;

namespace RedFox.Graphics3D;

/// <summary>
/// Represents an API-agnostic render handle that owns or references render-time state for a scene asset.
/// </summary>
public interface IRenderHandle : IDisposable
{
    /// <summary>
    /// Updates lazy resources, uploads dynamic data, and refreshes backend state.
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
    /// Releases backend resources owned by the handle.
    /// </summary>
    void Release();
}