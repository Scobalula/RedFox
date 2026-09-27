using RedFox.Graphics3D.Rendering;
using System;

namespace RedFox.Graphics3D.Rendering.Materials;

internal sealed class MaterialTypePipelineCacheEntry(IGpuPipelineState pipeline)
{
    public IGpuPipelineState Pipeline { get; } = pipeline ?? throw new ArgumentNullException(nameof(pipeline));

    public int ReferenceCount { get; set; } = 1;
}
