using System.Numerics;
using RedFox.Graphics3D.OpenGL.Shaders;
using RedFox.Graphics3D.OpenGL.Viewing;
using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.OpenGL.Passes;

public sealed class BonePass : IRenderPass
{
    private GL _gl = null!;
    private GLShader _shader = null!;
    private bool _isOpenGles;
    private bool _initialized;

    public string Name => "Bones";
    public PassPhase Phase => PassPhase.Pass;
    public bool Enabled { get; set; } = true;

    public void Initialize(GLRenderer renderer)
    {
        _gl = renderer.GL;
        _isOpenGles = renderer.Settings.IsOpenGles;

        (string vert, string frag) = ShaderSource.LoadProgram(_gl, "bone");
        _shader = new GLShader(_gl, vert, frag);
        _initialized = true;
    }

    public void Render(GLRenderer renderer, Scene scene, float deltaTime)
    {
        if (!_initialized || !Enabled || !renderer.Settings.ShowBones)
            return;

        Camera? camera = renderer.Settings.ActiveCamera;
        if (camera is null)
            return;

        RenderSettings settings = renderer.Settings;
        float axisScale = ComputeAxisScale(scene, settings.SceneTransform);

        _shader.Use();
        _shader.SetUniform("uView", camera.GetViewMatrix());
        _shader.SetUniform("uProjection", camera.GetProjectionMatrix());
        _shader.SetUniform("uScene", settings.SceneTransform);
        _shader.SetUniform("uAxisScale", axisScale);

        _gl.Disable(EnableCap.CullFace);
        _gl.Disable(EnableCap.DepthTest);
        _gl.DepthMask(false);
        if (!_isOpenGles)
            _gl.Enable(GLEnum.DepthClamp);

        foreach (Skeleton skeleton in scene.RootNode.EnumerateDescendants<Skeleton>())
            RenderSkeleton(renderer, skeleton);

        if (!_isOpenGles)
            _gl.Disable(GLEnum.DepthClamp);
        _gl.DepthMask(true);
        _gl.Enable(EnableCap.DepthTest);
        _gl.BindVertexArray(0);
        _gl.BindTexture(TextureTarget.Texture2D, 0);
    }

    public void Dispose()
    {
        _shader?.Dispose();
    }

    private void RenderSkeleton(GLRenderer renderer, Skeleton skeleton)
    {
        SkeletonRenderHandle? handle = renderer.GetOrCreateSkeletonHandle(skeleton);
        if (handle is null || handle.BoneCount == 0)
            return;

        handle.Update(renderer.GL, 0);
        handle.Draw(_gl, _shader);
    }

    private static float ComputeAxisScale(Scene scene, Matrix4x4 sceneTransform)
    {
        if (SceneBounds.TryGetBounds(scene, sceneTransform, out SceneBoundsInfo bounds) && bounds.Radius > 1e-6f)
            return MathF.Max(bounds.Radius * 0.04f, 1e-4f);

        return 1.0f;
    }
}
