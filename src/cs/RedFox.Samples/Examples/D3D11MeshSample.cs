using RedFox.Graphics3D.D3D11;

namespace RedFox.Samples.Examples;

/// <summary>
/// Runs a minimal Direct3D 11 mesh rendering demo using the RedFox scene graph.
/// </summary>
internal sealed class D3D11MeshSample : ISample
{
    /// <inheritdoc />
    public string Name => "graphics3d-d3d11-mesh";

    /// <inheritdoc />
    public string Description => "Opens a Direct3D 11 window and renders a lit untextured mesh scene from one or more supported translator files (or a fallback triangle).";

    /// <inheritdoc />
    public int Run(string[] arguments) => SilkMeshSampleRunner.Run(arguments, "RedFox D3D11 Mesh Sample", "D3D11 Scene", new D3D11SilkBackendFactory());
}
