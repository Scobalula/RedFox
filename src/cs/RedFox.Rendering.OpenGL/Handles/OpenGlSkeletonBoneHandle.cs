using RedFox.Graphics3D;
using RedFox.Graphics3D.OpenGL.Resources;
using System;
using System.Collections.Generic;
using System.Numerics;
using Silk.NET.OpenGL;

namespace RedFox.Rendering.OpenGL.Handles;

/// <summary>
/// OpenGL render handle for a <see cref="SkeletonBone"/> overlay. Owns the bone-axis VAO and
/// exposes <see cref="DrawOverlay"/> for the skeleton overlay pass.
/// </summary>
internal sealed class OpenGlSkeletonBoneHandle : ISceneNodeRenderHandle
{
    private readonly OpenGlContext _context;
    private readonly SkeletonBone _bone;
    private readonly OpenGlRenderSettings _settings;

    private uint _vao;
    private uint _vbo;
    private int _vertexCount = -1;
    private bool _disposed;

    public OpenGlSkeletonBoneHandle(OpenGlContext context, SkeletonBone bone, OpenGlRenderSettings settings)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _bone = bone ?? throw new ArgumentNullException(nameof(bone));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public SkeletonBone Bone => _bone;

    public bool IsOwnedBy(OpenGlContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ReferenceEquals(_context, context);
    }

    public void Update()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        float[] vertices = BuildLineVertices(_bone, _settings);
        _vertexCount = vertices.Length / 13;

        GL gl = _context.Gl;
        if (_vao == 0)
        {
            _vao = _context.CreateVertexArray();
            _vbo = _context.CreateBuffer();
            gl.BindVertexArray(_vao);
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
            gl.BufferData<float>(BufferTargetARB.ArrayBuffer, vertices, BufferUsageARB.DynamicDraw);
            OpenGlGridHandle.ConfigureLineVertexAttributes(gl);
            gl.BindVertexArray(0);
        }
        else
        {
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
            gl.BufferData<float>(BufferTargetARB.ArrayBuffer, vertices, BufferUsageARB.DynamicDraw);
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        }
    }

    public void DrawOverlay(GlShaderProgram lineShaderProgram, Matrix4x4 sceneAxisMatrix, Vector2 viewportSize, in CameraView view)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_vertexCount <= 0 || _vao == 0)
        {
            return;
        }

        lineShaderProgram.Use();
        lineShaderProgram.SetMatrix4("uModel", _bone.GetActiveWorldMatrix());
        lineShaderProgram.SetMatrix4("uSceneAxis", sceneAxisMatrix);
        lineShaderProgram.SetMatrix4("uView", view.ViewMatrix);
        lineShaderProgram.SetMatrix4("uProjection", view.ProjectionMatrix);
        lineShaderProgram.SetVector2("uViewportSize", viewportSize);
        lineShaderProgram.SetFloat("uLineHalfWidthPx", MathF.Max(_settings.BoneLineWidth, 0.75f) * 0.5f);
        lineShaderProgram.SetVector3("uCameraPosition", view.Position);
        lineShaderProgram.SetFloat("uFadeStartDistance", 0.0f);
        lineShaderProgram.SetFloat("uFadeEndDistance", 0.0f);

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

    private static float[] BuildLineVertices(SkeletonBone bone, OpenGlRenderSettings settings)
    {
        float axisSize = MathF.Max(settings.BoneAxisSize, 0.0f);
        List<float> output = new(312);
        Vector3 parentLocalOrigin = Vector3.Zero;
        Matrix4x4 inverseLocal = default;
        bool hasParent = bone.Parent is not null
            && Matrix4x4.Invert(bone.GetActiveLocalMatrix(), out inverseLocal);

        if (hasParent)
        {
            parentLocalOrigin = Vector3.Transform(Vector3.Zero, inverseLocal);
            float parentDistance = parentLocalOrigin.Length();
            if (parentDistance > 1e-6f)
            {
                float scaledFromParent = parentDistance * MathF.Max(settings.BoneAxisScaleFromParent, 0.0f);
                axisSize = MathF.Max(axisSize, scaledFromParent);
            }
        }

        if (settings.BoneAxisMaxSize > 0.0f)
        {
            axisSize = MathF.Min(axisSize, settings.BoneAxisMaxSize);
        }

        if (axisSize > 0.0f)
        {
            AddLineSegment(output, Vector3.Zero, Vector3.UnitX * axisSize, settings.BoneAxisXColor, 1.0f);
            AddLineSegment(output, Vector3.Zero, Vector3.UnitY * axisSize, settings.BoneAxisYColor, 1.0f);
            AddLineSegment(output, Vector3.Zero, Vector3.UnitZ * axisSize, settings.BoneAxisZColor, 1.0f);
        }

        if (hasParent)
        {
            Vector4 connectionColor = settings.UseBoneNameHashColor
                ? BuildBoneNameColor(bone.Name, settings)
                : settings.BoneConnectionColor;
            AddLineSegment(output, parentLocalOrigin, Vector3.Zero, connectionColor, 1.0f);
        }

        return output.ToArray();
    }

    private static Vector4 BuildBoneNameColor(string boneName, OpenGlRenderSettings settings)
    {
        string source = string.IsNullOrWhiteSpace(boneName) ? "Bone" : boneName;
        uint hash = 2166136261u;
        for (int i = 0; i < source.Length; i++)
        {
            hash ^= source[i];
            hash *= 16777619u;
        }

        float hue = (hash % 360u) / 360.0f;
        float saturation = Math.Clamp(settings.BoneNameColorSaturation, 0.0f, 1.0f);
        float value = Math.Clamp(settings.BoneNameColorValue, 0.0f, 1.0f);
        Vector3 rgb = HsvToRgb(hue, saturation, value);
        return new Vector4(rgb.X, rgb.Y, rgb.Z, settings.BoneConnectionColor.W);
    }

    private static Vector3 HsvToRgb(float hue, float saturation, float value)
    {
        if (saturation <= 0.0f)
        {
            return new Vector3(value, value, value);
        }

        float scaledHue = (hue - MathF.Floor(hue)) * 6.0f;
        int sextant = (int)scaledHue;
        float fraction = scaledHue - sextant;
        float p = value * (1.0f - saturation);
        float q = value * (1.0f - saturation * fraction);
        float t = value * (1.0f - saturation * (1.0f - fraction));

        return sextant switch
        {
            0 => new Vector3(value, t, p),
            1 => new Vector3(q, value, p),
            2 => new Vector3(p, value, t),
            3 => new Vector3(p, q, value),
            4 => new Vector3(t, p, value),
            _ => new Vector3(value, p, q),
        };
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
