using RedFox.Graphics3D.Rendering;

namespace RedFox.Graphics3D.Rendering.Materials;

internal interface IMaterialPipelineProvider
{
    IGpuPipelineState CreatePipeline(IGraphicsDevice graphicsDevice, string typeName);

    IGpuPipelineState AcquirePipeline(IGraphicsDevice graphicsDevice, string typeName);

    void ReleasePipeline(string typeName, IGpuPipelineState pipeline);
}