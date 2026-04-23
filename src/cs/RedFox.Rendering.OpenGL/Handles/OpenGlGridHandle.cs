using RedFox.Graphics3D;
using RedFox.Graphics3D.OpenGL.Resources;
using System;
using System.Collections.Generic;
using System.Numerics;
using Silk.NET.OpenGL;

namespace RedFox.Rendering.OpenGL.Handles;

/// <summary>
/// OpenGL render handle for a <see cref="Grid"/> scene node. Owns the line-expanded grid VAO
/// and exposes <see cref="DrawTransparent"/> for the transparent geometry pass.
/// </summary>
internal sealed class OpenGlGridHandle : ISceneNodeRenderHandle
{
    private readonly OpenGlContext _context;
    private readonly Grid _grid;

    private uint _vao;
    private uint _vbo;
    private int _vertexCount = -1;
    private float _lastSize = float.NaN;
    private float _lastSpacing;
    private int _lastMajorStep;
    private float _lastLineWidth;
    private float _lastEdgeLineWidthScale;
    private Vector4 _lastMinorColor;
    private Vector4 _lastMajorColor;
    private Vector4 _lastAxisXColor;
    private Vector4 _lastAxisZColor;
    private bool _disposed;

    public OpenGlGridHandle(OpenGlContext context, Grid grid)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _grid = grid ?? throw new ArgumentNullException(nameof(grid));
    }

    public Grid Grid => _grid;

    public bool IsOwnedBy(OpenGlContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ReferenceEquals(_context, context);
    }

    public void Update()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsGeometryStale(_grid))
        {
            RebuildGeometry(_grid);
        }
    }

    public void DrawTransparent(GlShaderProgram lineShaderProgram, Vector2 viewportSize, in CameraView view)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_vertexCount <= 0 || _vao == 0)
        {
            return;
        }

        Grid grid = _grid;
        float fadeStart = grid.FadeStartDistance > 0.0f ? grid.FadeStartDistance : grid.Size * 0.375f;
        float fadeEnd = grid.FadeEndDistance > fadeStart ? grid.FadeEndDistance : grid.Size * 1.25f;

        lineShaderProgram.Use();
        lineShaderProgram.SetMatrix4("uModel", Matrix4x4.Identity);
        lineShaderProgram.SetMatrix4("uSceneAxis", Matrix4x4.Identity);
        lineShaderProgram.SetMatrix4("uView", view.ViewMatrix);
        lineShaderProgram.SetMatrix4("uProjection", view.ProjectionMatrix);
        lineShaderProgram.SetVector2("uViewportSize", viewportSize);
        lineShaderProgram.SetFloat("uLineHalfWidthPx", MathF.Max(grid.LineWidth, 0.75f) * 0.5f);
        lineShaderProgram.SetVector3("uCameraPosition", view.Position);
        lineShaderProgram.SetFloat("uFadeStartDistance", grid.FadeEnabled ? fadeStart : 0.0f);
        lineShaderProgram.SetFloat("uFadeEndDistance", grid.FadeEnabled ? fadeEnd : 0.0f);

        GL gl = _context.Gl;
        gl.BindVertexArray(_vao);
        gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)_vertexCount);
        gl.BindVertexArray(0);
    }

    public void Release()
    {
        if (_disposed)
        {
            return;
        }

        if (_vao != 0)
        {
            _context.DeleteVertexArray(_vao);
            _vao = 0;
        }

        if (_vbo != 0)
        {
            _context.DeleteBuffer(_vbo);
            _vbo = 0;
        }

        _vertexCount = -1;
        _disposed = true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _vao = 0;
        _vbo = 0;
        _vertexCount = -1;
        _disposed = true;
    }

    private bool IsGeometryStale(Grid grid) =>
        _vertexCount < 0
        || _vao == 0
        || _lastSize != grid.Size
        || _lastSpacing != grid.Spacing
        || _lastMajorStep != grid.MajorStep
        || _lastLineWidth != grid.LineWidth
        || _lastEdgeLineWidthScale != grid.EdgeLineWidthScale
        || _lastMinorColor != grid.MinorColor
        || _lastMajorColor != grid.MajorColor
        || _lastAxisXColor != grid.AxisXColor
        || _lastAxisZColor != grid.AxisZColor;

    private void RebuildGeometry(Grid grid)
    {
        float[] vertices = BuildLineVertices(grid);
        _vertexCount = vertices.Length / 13;

        GL gl = _context.Gl;
        if (_vao == 0)
        {
            _vao = _context.CreateVertexArray();
            _vbo = _context.CreateBuffer();
            gl.BindVertexArray(_vao);
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
            gl.BufferData<float>(BufferTargetARB.ArrayBuffer, vertices, BufferUsageARB.DynamicDraw);
            ConfigureLineVertexAttributes(gl);
            gl.BindVertexArray(0);
        }
        else
        {
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
            gl.BufferData<float>(BufferTargetARB.ArrayBuffer, vertices, BufferUsageARB.DynamicDraw);
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        }

        _lastSize = grid.Size;
        _lastSpacing = grid.Spacing;
        _lastMajorStep = grid.MajorStep;
        _lastLineWidth = grid.LineWidth;
        _lastEdgeLineWidthScale = grid.EdgeLineWidthScale;
        _lastMinorColor = grid.MinorColor;
        _lastMajorColor = grid.MajorColor;
        _lastAxisXColor = grid.AxisXColor;
        _lastAxisZColor = grid.AxisZColor;
    }

    internal static unsafe void ConfigureLineVertexAttributes(GL gl)
    {
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 52, (void*)0);
        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 52, (void*)12);
        gl.EnableVertexAttribArray(2);
        gl.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false, 52, (void*)24);
        gl.EnableVertexAttribArray(3);
        gl.VertexAttribPointer(3, 1, VertexAttribPointerType.Float, false, 52, (void*)40);
        gl.EnableVertexAttribArray(4);
        gl.VertexAttribPointer(4, 1, VertexAttribPointerType.Float, false, 52, (void*)44);
        gl.EnableVertexAttribArray(5);
        gl.VertexAttribPointer(5, 1, VertexAttribPointerType.Float, false, 52, (void*)48);
    }

    private static float[] BuildLineVertices(Grid grid)
    {
        float size = MathF.Max(grid.Size, grid.Spacing);
        float spacing = MathF.Max(grid.Spacing, 0.0001f);
        int majorStep = Math.Max(1, grid.MajorStep);
        Vector4 minorColor = grid.MinorColor;
        Vector4 majorColor = grid.MajorColor;
        Vector4 axisXColor = grid.AxisXColor;
        Vector4 axisZColor = grid.AxisZColor;

        int half = (int)MathF.Ceiling(size / spacing);
        float extent = half * spacing;
        int capacity = 2 * (2 * half + 1) * 6 * 13;
        List<float> output = new(capacity);

        for (int i = -half; i <= half; i++)
        {
            float x = i * spacing;
            int abs = Math.Abs(i);
            bool isAxis = i == 0;
            bool isEdge = abs == half;
            bool isInnerEdge = abs == half - 1 && grid.EdgeLineWidthScale > 1.0f;
            bool isMajor = !isAxis && !isInnerEdge && abs % majorStep == 0;
            Vector4 color = isAxis ? axisZColor : (isMajor ? majorColor : minorColor);
            float widthScale = isEdge ? MathF.Max(grid.EdgeLineWidthScale, 1.0f) : 1.0f;
            AddLineSegment(output, new Vector3(x, 0.0f, -extent), new Vector3(x, 0.0f, extent), color, widthScale);
        }

        for (int j = -half; j <= half; j++)
        {
            float z = j * spacing;
            int abs = Math.Abs(j);
            bool isAxis = j == 0;
            bool isEdge = abs == half;
            bool isInnerEdge = abs == half - 1 && grid.EdgeLineWidthScale > 1.0f;
            bool isMajor = !isAxis && !isInnerEdge && abs % majorStep == 0;
            Vector4 color = isAxis ? axisXColor : (isMajor ? majorColor : minorColor);
            float widthScale = isEdge ? MathF.Max(grid.EdgeLineWidthScale, 1.0f) : 1.0f;
            AddLineSegment(output, new Vector3(-extent, 0.0f, z), new Vector3(extent, 0.0f, z), color, widthScale);
        }

        return output.ToArray();
    }

    private static void AddLineSegment(List<float> vertices, Vector3 start, Vector3 end, Vector4 color, float widthScale)
    {
        AddExpandedVertex(vertices, start, end, color, 0.0f, -1.0f, widthScale);
        AddExpandedVertex(vertices, start, end, color, 0.0f, 1.0f, widthScale);
        AddExpandedVertex(vertices, start, end, color, 1.0f, 1.0f, widthScale);
        AddExpandedVertex(vertices, start, end, color, 0.0f, -1.0f, widthScale);
        AddExpandedVertex(vertices, start, end, color, 1.0f, 1.0f, widthScale);
        AddExpandedVertex(vertices, start, end, color, 1.0f, -1.0f, widthScale);
    }

    private static void AddExpandedVertex(List<float> vertices, Vector3 start, Vector3 end, Vector4 color, float along, float side, float widthScale)
    {
        vertices.Add(start.X);
        vertices.Add(start.Y);
        vertices.Add(start.Z);
        vertices.Add(end.X);
        vertices.Add(end.Y);
        vertices.Add(end.Z);
        vertices.Add(color.X);
        vertices.Add(color.Y);
        vertices.Add(color.Z);
        vertices.Add(color.W);
        vertices.Add(along);
        vertices.Add(side);
        vertices.Add(widthScale);
    }
}
