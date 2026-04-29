using System;
using RedFox.Graphics3D.Rendering;
using RedFox.Graphics3D.Rendering.Materials;

namespace RedFox.Graphics3D.D3D11;

internal sealed class D3D11MaterialShaderFactory : IMaterialShaderFactory
{
    internal static D3D11MaterialShaderFactory Instance { get; } = new();

    private D3D11MaterialShaderFactory()
    {
    }

    IGpuShader IMaterialShaderFactory.CreateShader(IGraphicsDevice graphicsDevice, string shaderName, ShaderStage stage)
    {
        D3D11GraphicsDevice d3dGraphicsDevice = graphicsDevice as D3D11GraphicsDevice
            ?? throw new InvalidOperationException($"Expected {nameof(D3D11GraphicsDevice)}.");
        ArgumentException.ThrowIfNullOrWhiteSpace(shaderName);

        string shaderPath = stage switch
        {
            ShaderStage.Vertex => HlslShaderSourceLoader.GetVertexSourcePath(shaderName),
            ShaderStage.Fragment => HlslShaderSourceLoader.GetFragmentSourcePath(shaderName),
            ShaderStage.Compute => HlslShaderSourceLoader.GetComputeSourcePath(shaderName),
            _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unsupported shader stage."),
        };

        return d3dGraphicsDevice.CreateShaderFromFile(shaderPath, stage);
    }
}