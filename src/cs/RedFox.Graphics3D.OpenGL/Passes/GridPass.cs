using System.Numerics;
using RedFox.Graphics3D.OpenGL.Shaders;
using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.OpenGL.Passes;

public sealed class GridPass : IRenderPass
{
    private GL _gl = null!;
    private GLShader _shader = null!;
    private uint _vao;
    private uint _vbo;
    private int _vertexCount;
    private bool _initialized;

    public string Name => "Grid";
    public bool Enabled { get; set; } = true;
    public float HalfExtent { get; set; } = 10.0f;
    public float Step { get; set; } = 1.0f;

    public void Initialize(GLRenderer renderer)
    {
        _gl = renderer.GL;
        (string gridVertex, string gridFragment) = ShaderSource.LoadProgram(_gl, "grid");
        _shader = new GLShader(_gl, gridVertex, gridFragment);
        BuildGeometry();
        _initialized = true;
    }

    public void Render(GLRenderer renderer, Scene scene, float deltaTime)
    {
        if (!_initialized || !Enabled || renderer.ActiveCamera is null || _vao == 0)
            return;

        _shader.Use();
        // System.Numerics composes row-major transforms, so combined matrices
        // need to be built as view * projection before they are uploaded.
        _shader.SetUniform("uViewProjection", renderer.ActiveCamera.GetViewMatrix() * renderer.ActiveCamera.GetProjectionMatrix());
        _shader.SetUniform("uFarPlane", renderer.ActiveCamera.FarPlane);

        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        _gl.BindVertexArray(_vao);
        _gl.DrawArrays(PrimitiveType.Lines, 0, (uint)_vertexCount);
        _gl.BindVertexArray(0);
        _gl.Disable(EnableCap.Blend);
    }

    private unsafe void BuildGeometry()
    {
        List<float> vertices = [];
        int lineCount = (int)MathF.Floor((HalfExtent * 2.0f) / Step) + 1;

        for (int lineIndex = 0; lineIndex < lineCount; lineIndex++)
        {
            float offset = -HalfExtent + (lineIndex * Step);
            Vector4 color = MathF.Abs(offset) < 1e-4f
                ? new Vector4(0.56f, 0.56f, 0.60f, 0.9f)
                : new Vector4(0.32f, 0.34f, 0.38f, 0.55f);

            AddVertex(vertices, new Vector3(offset, 0.0f, -HalfExtent), color);
            AddVertex(vertices, new Vector3(offset, 0.0f, HalfExtent), color);
            AddVertex(vertices, new Vector3(-HalfExtent, 0.0f, offset), color);
            AddVertex(vertices, new Vector3(HalfExtent, 0.0f, offset), color);
        }

        AddVertex(vertices, Vector3.Zero, new Vector4(0.95f, 0.25f, 0.25f, 1.0f));
        AddVertex(vertices, new Vector3(HalfExtent, 0.0f, 0.0f), new Vector4(0.95f, 0.25f, 0.25f, 1.0f));
        AddVertex(vertices, Vector3.Zero, new Vector4(0.25f, 0.6f, 0.95f, 1.0f));
        AddVertex(vertices, new Vector3(0.0f, 0.0f, HalfExtent), new Vector4(0.25f, 0.6f, 0.95f, 1.0f));

        float[] data = [.. vertices];
        _vertexCount = data.Length / 7;

        _vao = _gl.GenVertexArray();
        _vbo = _gl.GenBuffer();

        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        fixed (float* ptr = data)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(data.Length * sizeof(float)), ptr, BufferUsageARB.StaticDraw);
        }

        const uint stride = 7 * sizeof(float);
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
        _gl.EnableVertexAttribArray(1);
        _gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));
        _gl.BindVertexArray(0);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
    }

    private static void AddVertex(List<float> vertices, Vector3 position, Vector4 color)
    {
        vertices.Add(position.X);
        vertices.Add(position.Y);
        vertices.Add(position.Z);
        vertices.Add(color.X);
        vertices.Add(color.Y);
        vertices.Add(color.Z);
        vertices.Add(color.W);
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
}
