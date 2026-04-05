using System.Numerics;
using RedFox.Graphics3D.OpenGL.Shaders;
using RedFox.Graphics3D.OpenGL.Viewing;
using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.OpenGL.Passes;

public sealed class GridPass : IRenderPass
{
    private GL _gl = null!;
    private GLShader _shader = null!;
    private uint _vao;
    private uint _vbo;
    private GridSegment[] _segments = [];
    private bool _initialized;

    public string Name => "Grid";
    public bool Enabled { get; set; } = true;
    public float HalfExtent { get; set; } = 10.0f;
    public float Step { get; set; } = 1.0f;

    public void Initialize(GLRenderer renderer)
    {
        _gl = renderer.GL;
        (string gridVertex, string gridFragment) = ShaderSource.LoadProgram(_gl, "overlay_line");
        _shader = new GLShader(_gl, gridVertex, gridFragment);
        BuildGeometry();
        _initialized = true;
    }

    public unsafe void Render(GLRenderer renderer, Scene scene, float deltaTime)
    {
        if (!_initialized || !Enabled || renderer.ActiveCamera is null || _vao == 0)
            return;

        int* viewport = stackalloc int[4];
        _gl.GetInteger(GLEnum.Viewport, viewport);
        int viewportWidth = viewport[2];
        int viewportHeight = viewport[3];
        if (viewportWidth <= 0 || viewportHeight <= 0)
            return;

        Matrix4x4 viewMatrix = renderer.ActiveCamera.GetViewMatrix();
        Matrix4x4 projectionMatrix = renderer.ActiveCamera.GetProjectionMatrix();
        float nearPlane = renderer.ActiveCamera.NearPlane;
        float farPlane = renderer.ActiveCamera.FarPlane;
        List<float> vertices = new(_segments.Length * 48);
        foreach (GridSegment segment in _segments)
        {
            ScreenSpaceLineMeshBuilder.TryAppendQuad(
                vertices,
                segment.Start,
                segment.End,
                viewMatrix,
                projectionMatrix,
                nearPlane,
                farPlane,
                viewportWidth,
                viewportHeight,
                1.25f,
                segment.Color);
        }

        if (vertices.Count == 0)
            return;

        float[] data = [.. vertices];
        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        fixed (float* ptr = data)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(data.Length * sizeof(float)), ptr, BufferUsageARB.DynamicDraw);
        }

        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 4, VertexAttribPointerType.Float, false, 8 * sizeof(float), 0);
        _gl.EnableVertexAttribArray(1);
        _gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, 8 * sizeof(float), 4 * sizeof(float));

        _shader.Use();
        _gl.Disable(EnableCap.CullFace);
        _gl.Disable(EnableCap.DepthTest);
        _gl.DepthMask(false);
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)(data.Length / 8));
        _gl.BindVertexArray(0);
        _gl.Disable(EnableCap.Blend);
        _gl.DepthMask(true);
    }

    private void BuildGeometry()
    {
        List<GridSegment> segments = [];
        int lineCount = (int)MathF.Floor((HalfExtent * 2.0f) / Step) + 1;

        for (int lineIndex = 0; lineIndex < lineCount; lineIndex++)
        {
            float offset = -HalfExtent + (lineIndex * Step);
            Vector4 color = MathF.Abs(offset) < 1e-4f
                ? new Vector4(0.56f, 0.56f, 0.60f, 0.9f)
                : new Vector4(0.32f, 0.34f, 0.38f, 0.55f);

            segments.Add(new GridSegment(new Vector3(offset, 0.0f, -HalfExtent), new Vector3(offset, 0.0f, HalfExtent), color));
            segments.Add(new GridSegment(new Vector3(-HalfExtent, 0.0f, offset), new Vector3(HalfExtent, 0.0f, offset), color));
        }

        segments.Add(new GridSegment(Vector3.Zero, new Vector3(HalfExtent, 0.0f, 0.0f), new Vector4(0.95f, 0.25f, 0.25f, 1.0f)));
        segments.Add(new GridSegment(Vector3.Zero, new Vector3(0.0f, 0.0f, HalfExtent), new Vector4(0.25f, 0.6f, 0.95f, 1.0f)));
        _segments = [.. segments];

        _vao = _gl.GenVertexArray();
        _vbo = _gl.GenBuffer();

        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        _gl.BindVertexArray(0);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
    }

    public void Dispose()
    {
        _shader?.Dispose();

        try
        {
            if (_vao != 0)
                _gl.DeleteVertexArray(_vao);

            if (_vbo != 0)
                _gl.DeleteBuffer(_vbo);
        }
        catch
        {
        }
    }

    private readonly record struct GridSegment(Vector3 Start, Vector3 End, Vector4 Color);
}
