using System;
using System.Text;
using RedFox.Graphics3D.OpenGL.Shaders;
using RedFox.Graphics3D.Rendering;
using RedFox.Graphics3D.Rendering.Materials;

namespace RedFox.Graphics3D.OpenGL;

internal sealed class OpenGlMaterialShaderFactory : IMaterialShaderFactory
{
    internal static OpenGlMaterialShaderFactory Instance { get; } = new();

    private OpenGlMaterialShaderFactory()
    {
    }

    IGpuShader IMaterialShaderFactory.CreateShader(IGraphicsDevice graphicsDevice, string shaderName, ShaderStage stage)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentException.ThrowIfNullOrWhiteSpace(shaderName);

        string source = stage switch
        {
            ShaderStage.Vertex => GlslShaderSourceLoader.LoadVertexSource(shaderName),
            ShaderStage.Fragment => GlslShaderSourceLoader.LoadFragmentSource(shaderName),
            ShaderStage.Compute => GlslShaderSourceLoader.LoadComputeSource(shaderName),
            _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unsupported shader stage."),
        };

        return graphicsDevice.CreateShader(Encoding.UTF8.GetBytes(source), stage);
    }
}