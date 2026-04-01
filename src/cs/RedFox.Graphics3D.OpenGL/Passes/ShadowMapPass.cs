namespace RedFox.Graphics3D.OpenGL.Passes;

public sealed class ShadowMapPass : IRenderPass
{
    public string Name => "ShadowMap";
    public bool Enabled { get; set; }

    public void Initialize(GLRenderer renderer)
    {
    }

    public void Render(GLRenderer renderer, Scene scene, float deltaTime)
    {
    }

    public void Dispose()
    {
    }
}
