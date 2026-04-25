using System;
using System.Numerics;
using System.Runtime.InteropServices;
using RedFox.Graphics3D.Rendering.Backend;
using RedFox.Graphics3D.Rendering.Materials;

namespace RedFox.Graphics3D.Rendering.Handles;

/// <summary>
/// Owns grid line geometry and pipeline state for the backend renderer.
/// </summary>
internal sealed class GridRenderHandle : RenderHandle
{
    private const int FloatCountPerVertex = 13;
    private const int LineAttributeSlotCount = 6;

    private readonly IGraphicsDevice _graphicsDevice;
    private readonly Grid _grid;
    private readonly IMaterialTypeRegistry _materialTypes;

    private IGpuBuffer? _lineBuffer;
    private int _lineBufferSizeBytes;
    private float _lastEdgeLineWidthScale;
    private Vector4 _lastAxisXColor;
    private Vector4 _lastAxisZColor;
    private float _lastLineWidth;
    private Vector4 _lastMajorColor;
    private int _lastMajorStep;
    private Vector4 _lastMinorColor;
    private float _lastSize = float.NaN;
    private float _lastSpacing;
    private IGpuPipelineState? _pipeline;
    private Matrix4x4 _worldMatrix;
    private int _vertexCount = -1;

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

        EnsurePipeline();
        _worldMatrix = _grid.GetActiveWorldMatrix();

        if (!IsGeometryStale(_grid))
        {
            return;
        }

        float[] vertices = BuildLineVertices(_grid);
        _vertexCount = vertices.Length / FloatCountPerVertex;

        ReadOnlySpan<byte> vertexBytes = MemoryMarshal.AsBytes(vertices.AsSpan());
        if (_lineBuffer is null || _lineBufferSizeBytes != vertexBytes.Length)
        {
            _lineBuffer?.Dispose();
            _lineBuffer = _graphicsDevice.CreateBuffer(
                vertexBytes.Length,
                FloatCountPerVertex * sizeof(float),
                BufferUsage.Vertex | BufferUsage.DynamicWrite,
                vertexBytes);
            _lineBufferSizeBytes = vertexBytes.Length;
        }
        else
        {
            _graphicsDevice.UpdateBuffer(_lineBuffer, vertexBytes);
        }

        _lastSize = _grid.Size;
        _lastSpacing = _grid.Spacing;
        _lastMajorStep = _grid.MajorStep;
        _lastLineWidth = _grid.LineWidth;
        _lastEdgeLineWidthScale = _grid.EdgeLineWidthScale;
        _lastMinorColor = _grid.MinorColor;
        _lastMajorColor = _grid.MajorColor;
        _lastAxisXColor = _grid.AxisXColor;
        _lastAxisZColor = _grid.AxisZColor;
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

        if (phase != RenderPhase.Transparent || _pipeline is null || _lineBuffer is null || _vertexCount <= 0)
        {
            return;
        }

        commandList.SetPipelineState(_pipeline);
        BindLineVertexBuffer(commandList, _lineBuffer);
        commandList.SetUniformMatrix4x4("Model", _worldMatrix);
        commandList.SetUniformMatrix4x4("SceneAxis", Matrix4x4.Identity);
        commandList.SetUniformMatrix4x4("View", view);
        commandList.SetUniformMatrix4x4("Projection", projection);
        commandList.SetUniformVector2("ViewportSize", viewportSize);
        commandList.SetUniformFloat("LineHalfWidthPx", MathF.Max(_grid.LineWidth, 0.75f) * 0.5f);
        commandList.SetUniformVector3("CameraPosition", cameraPosition);
        commandList.SetUniformFloat("FadeStartDistance", _grid.FadeEnabled ? ResolveFadeStartDistance(_grid) : 0.0f);
        commandList.SetUniformFloat("FadeEndDistance", _grid.FadeEnabled ? ResolveFadeEndDistance(_grid) : 0.0f);
        commandList.Draw(_vertexCount, 0);
    }

    /// <inheritdoc/>
    protected override void ReleaseCore()
    {
        _pipeline?.Dispose();
        _pipeline = null;
        _lineBuffer?.Dispose();
        _lineBuffer = null;
        _lineBufferSizeBytes = 0;
        _vertexCount = -1;
    }

    private bool IsGeometryStale(Grid grid)
    {
        return _vertexCount < 0
            || _lineBuffer is null
            || _lastSize != grid.Size
            || _lastSpacing != grid.Spacing
            || _lastMajorStep != grid.MajorStep
            || _lastLineWidth != grid.LineWidth
            || _lastEdgeLineWidthScale != grid.EdgeLineWidthScale
            || _lastMinorColor != grid.MinorColor
            || _lastMajorColor != grid.MajorColor
            || _lastAxisXColor != grid.AxisXColor
            || _lastAxisZColor != grid.AxisZColor;
    }

    private static void AddExpandedVertex(Span<float> destination, ref int offset, Vector3 start, Vector3 end, Vector4 color, float along, float side, float widthScale)
    {
        destination[offset++] = start.X;
        destination[offset++] = start.Y;
        destination[offset++] = start.Z;
        destination[offset++] = end.X;
        destination[offset++] = end.Y;
        destination[offset++] = end.Z;
        destination[offset++] = color.X;
        destination[offset++] = color.Y;
        destination[offset++] = color.Z;
        destination[offset++] = color.W;
        destination[offset++] = along;
        destination[offset++] = side;
        destination[offset++] = widthScale;
    }

    private static void AddLineSegment(Span<float> destination, ref int offset, Vector3 start, Vector3 end, Vector4 color, float widthScale)
    {
        AddExpandedVertex(destination, ref offset, start, end, color, 0.0f, -1.0f, widthScale);
        AddExpandedVertex(destination, ref offset, start, end, color, 0.0f, 1.0f, widthScale);
        AddExpandedVertex(destination, ref offset, start, end, color, 1.0f, 1.0f, widthScale);
        AddExpandedVertex(destination, ref offset, start, end, color, 0.0f, -1.0f, widthScale);
        AddExpandedVertex(destination, ref offset, start, end, color, 1.0f, 1.0f, widthScale);
        AddExpandedVertex(destination, ref offset, start, end, color, 1.0f, -1.0f, widthScale);
    }

    private static void BindLineVertexBuffer(ICommandList commandList, IGpuBuffer lineBuffer)
    {
        for (int slot = 0; slot < LineAttributeSlotCount; slot++)
        {
            commandList.BindBuffer(slot, lineBuffer);
        }
    }

    private static float[] BuildLineVertices(Grid grid)
    {
        float size = MathF.Max(grid.Size, grid.Spacing);
        float spacing = MathF.Max(grid.Spacing, 0.0001f);
        int majorStep = Math.Max(1, grid.MajorStep);
        int half = (int)MathF.Ceiling(size / spacing);
        int lineCount = ((2 * half) + 1) * 2;
        float extent = half * spacing;

        float[] vertices = new float[lineCount * 6 * FloatCountPerVertex];
        int offset = 0;

        for (int i = -half; i <= half; i++)
        {
            float x = i * spacing;
            int abs = Math.Abs(i);
            bool isAxis = i == 0;
            bool isEdge = abs == half;
            bool isInnerEdge = abs == half - 1 && grid.EdgeLineWidthScale > 1.0f;
            bool isMajor = !isAxis && !isInnerEdge && abs % majorStep == 0;
            Vector4 color = isAxis ? grid.AxisZColor : (isMajor ? grid.MajorColor : grid.MinorColor);
            float widthScale = isEdge ? MathF.Max(grid.EdgeLineWidthScale, 1.0f) : 1.0f;
            AddLineSegment(vertices, ref offset, new Vector3(x, 0.0f, -extent), new Vector3(x, 0.0f, extent), color, widthScale);
        }

        for (int j = -half; j <= half; j++)
        {
            float z = j * spacing;
            int abs = Math.Abs(j);
            bool isAxis = j == 0;
            bool isEdge = abs == half;
            bool isInnerEdge = abs == half - 1 && grid.EdgeLineWidthScale > 1.0f;
            bool isMajor = !isAxis && !isInnerEdge && abs % majorStep == 0;
            Vector4 color = isAxis ? grid.AxisXColor : (isMajor ? grid.MajorColor : grid.MinorColor);
            float widthScale = isEdge ? MathF.Max(grid.EdgeLineWidthScale, 1.0f) : 1.0f;
            AddLineSegment(vertices, ref offset, new Vector3(-extent, 0.0f, z), new Vector3(extent, 0.0f, z), color, widthScale);
        }

        return vertices;
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
        return grid.FadeEndDistance > fadeStart ? grid.FadeEndDistance : grid.Size * 1.25f;
    }

    private static float ResolveFadeStartDistance(Grid grid)
    {
        return grid.FadeStartDistance > 0.0f ? grid.FadeStartDistance : grid.Size * 0.375f;
    }
}