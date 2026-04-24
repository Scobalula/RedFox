using System;
using System.Numerics;
using RedFox.Graphics3D.Rendering.Backend;

namespace RedFox.Graphics3D.Rendering.Handles;

/// <summary>
/// Stores grid render state for the generic renderer pipeline.
/// </summary>
internal sealed class GridRenderHandle : RenderHandle
{
    private readonly Grid _grid;

    private Matrix4x4 _worldMatrix;

    /// <summary>
    /// Initializes a new instance of the <see cref="GridRenderHandle"/> class.
    /// </summary>
    /// <param name="grid">The grid node represented by this handle.</param>
    public GridRenderHandle(Grid grid)
    {
        _grid = grid ?? throw new ArgumentNullException(nameof(grid));
    }

    /// <inheritdoc/>
    public override void Update(ICommandList commandList)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(commandList);
        _worldMatrix = _grid.GetActiveWorldMatrix();
    }

    /// <inheritdoc/>
    public override void Render(
        ICommandList commandList,
        RenderPhase phase,
        in Matrix4x4 view,
        in Matrix4x4 projection,
        in Matrix4x4 sceneAxis,
        Vector3 cameraPosition,
        Vector2 viewportSize)
    {
        ThrowIfDisposed();

        if (phase != RenderPhase.Transparent)
        {
            return;
        }

        commandList.SetUniformMatrix4x4("uModel", _worldMatrix);
        commandList.SetUniformVector4("uGridMinorColor", _grid.MinorColor);
        commandList.SetUniformVector4("uGridMajorColor", _grid.MajorColor);
        commandList.SetUniformVector4("uGridAxisXColor", _grid.AxisXColor);
        commandList.SetUniformVector4("uGridAxisZColor", _grid.AxisZColor);
        commandList.SetUniformFloat("uGridSize", _grid.Size);
        commandList.SetUniformFloat("uGridSpacing", _grid.Spacing);
    }
}