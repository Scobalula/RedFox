using System;
using System.Numerics;
using RedFox.Graphics3D.Rendering.Backend;
using RedFox.Graphics3D.Rendering.Materials;

namespace RedFox.Graphics3D.Rendering.Handles;

/// <summary>
/// Owns scene grid pipeline state for the backend renderer.
/// </summary>
internal sealed class GridRenderHandle : RenderHandle
{
    private const int GridVertexCount = 6;
    private const float MinimumCellSize = 0.0001f;
    private const float MinimumLineWidth = 0.25f;
    private const float MinimumPixelsBetweenCells = 1.0f;

    private readonly IGraphicsDevice _graphicsDevice;
    private readonly Grid _grid;
    private readonly IMaterialTypeRegistry _materialTypes;

    private IGpuPipelineState? _pipeline;

    /// <summary>
    /// Initializes a new instance of the <see cref="GridRenderHandle"/> class.
    /// </summary>
    /// <param name="graphicsDevice">The graphics device that owns line resources.</param>
    /// <param name="materialTypes">The material registry used to resolve the grid pipeline.</param>
    /// <param name="grid">The grid node represented by this handle.</param>
    public GridRenderHandle(IGraphicsDevice graphicsDevice, IMaterialTypeRegistry materialTypes, Grid grid)
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        _materialTypes = materialTypes ?? throw new ArgumentNullException(nameof(materialTypes));
        _grid = grid ?? throw new ArgumentNullException(nameof(grid));
    }

    /// <inheritdoc/>
    public override void Update(ICommandList commandList)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(commandList);

        if (_grid.Enabled)
        {
            EnsurePipeline();
        }
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

        if (phase != RenderPhase.Transparent || !_grid.Enabled || _pipeline is null)
        {
            return;
        }

        commandList.SetPipelineState(_pipeline);
        commandList.SetUniformMatrix4x4("View", view);
        commandList.SetUniformMatrix4x4("Projection", projection);
        commandList.SetUniformVector3("CameraPosition", cameraPosition);
        commandList.SetUniformFloat("GridSize", ResolveGridSize(_grid));
        commandList.SetUniformFloat("GridCellSize", ResolveCellSize(_grid));
        commandList.SetUniformFloat("GridMajorStep", MathF.Max(_grid.MajorStep, 2));
        commandList.SetUniformFloat("GridMinPixelsBetweenCells", MathF.Max(_grid.MinimumPixelsBetweenCells, MinimumPixelsBetweenCells));
        commandList.SetUniformFloat("GridLineWidth", MathF.Max(_grid.LineWidth, MinimumLineWidth));
        commandList.SetUniformVector4("GridMinorColor", _grid.MinorColor);
        commandList.SetUniformVector4("GridMajorColor", _grid.MajorColor);
        commandList.SetUniformVector4("GridAxisXColor", _grid.AxisXColor);
        commandList.SetUniformVector4("GridAxisZColor", _grid.AxisZColor);
        commandList.SetUniformFloat("FadeStartDistance", _grid.FadeEnabled ? ResolveFadeStartDistance(_grid) : 0.0f);
        commandList.SetUniformFloat("FadeEndDistance", _grid.FadeEnabled ? ResolveFadeEndDistance(_grid) : 0.0f);
        commandList.Draw(GridVertexCount, 0);
    }

    /// <inheritdoc/>
    protected override void ReleaseCore()
    {
        _pipeline?.Dispose();
        _pipeline = null;
    }

    private void EnsurePipeline()
    {
        if (_pipeline is not null)
        {
            return;
        }

        MaterialTypeDefinition definition = _materialTypes.Get("Grid");
        _pipeline = definition.BuildPipeline(_graphicsDevice);
    }

    private static float ResolveFadeEndDistance(Grid grid)
    {
        float fadeStart = ResolveFadeStartDistance(grid);
        return grid.FadeEndDistance > fadeStart ? grid.FadeEndDistance : ResolveGridSize(grid) * 1.25f;
    }

    private static float ResolveFadeStartDistance(Grid grid)
    {
        return grid.FadeStartDistance > 0.0f ? grid.FadeStartDistance : ResolveGridSize(grid) * 0.375f;
    }

    private static float ResolveGridSize(Grid grid)
    {
        return MathF.Max(grid.Size, ResolveCellSize(grid));
    }

    private static float ResolveCellSize(Grid grid)
    {
        return MathF.Max(grid.Spacing, MinimumCellSize);
    }
}