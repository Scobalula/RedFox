using RedFox.Graphics3D.Rendering;
using System;

namespace RedFox.Graphics3D.Rendering.Materials;

internal sealed class MaterialTypePipelineCacheEntry
{
    public MaterialTypePipelineCacheEntry(IGpuPipelineState pipeline)
    {
        Pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        ReferenceCount = 1;
    }

    public IGpuPipelineState Pipeline { get; }

    public int ReferenceCount { get; set; }
}