using RedFox.Graphics3D.OpenGL.Resources;
using RedFox.Rendering.OpenGL.Shaders;
using System;
using System.Numerics;

namespace RedFox.Rendering.OpenGL;

/// <summary>
/// Shared GPU resources owned by an <see cref="OpenGlSceneRenderer"/> and consumed by its
/// passes. Created once during <see cref="OpenGlSceneRenderer.Initialize"/> and disposed
/// when the renderer is disposed.
/// </summary>
internal sealed class OpenGlRenderResources : IDisposable
{
    private bool _disposed;

    public OpenGlRenderResources(OpenGlContext context, OpenGlRenderSettings settings)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));

        MeshShaderProgram = context.CreateShaderProgram(BasicShaders.MeshVertexShaderSource, BasicShaders.MeshFragmentShaderSource);
        SkinningComputeProgram = context.CreateComputeProgram(BasicShaders.SkinningComputeShaderSource);
        LineShaderProgram = context.CreateShaderProgram(BasicShaders.LineVertexShaderSource, BasicShaders.LineFragmentShaderSource);
    }

    public OpenGlContext Context { get; }

    public OpenGlRenderSettings Settings { get; }

    public GlShaderProgram MeshShaderProgram { get; }

    public GlComputeProgram SkinningComputeProgram { get; }

    public GlShaderProgram LineShaderProgram { get; }

    public Vector2 ViewportSize { get; set; } = Vector2.One;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        LineShaderProgram.Dispose();
        SkinningComputeProgram.Dispose();
        MeshShaderProgram.Dispose();
        _disposed = true;
    }
}
