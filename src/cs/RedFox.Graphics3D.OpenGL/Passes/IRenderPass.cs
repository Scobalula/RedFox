using RedFox.Graphics3D;

namespace RedFox.Graphics3D.OpenGL.Passes;

public interface IRenderPass : IDisposable
{
    string Name { get; }

    PassPhase Phase { get; }

    bool Enabled { get; set; }

    void Initialize(GLRenderer renderer);

    void Render(GLRenderer renderer, Scene scene, float deltaTime);
}
