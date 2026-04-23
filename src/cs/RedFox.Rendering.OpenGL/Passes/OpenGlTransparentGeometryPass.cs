using RedFox.Rendering.OpenGL.Handles;
using RedFox.Rendering.Passes;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace RedFox.Rendering.OpenGL.Passes;

/// <summary>
/// Transparent-phase pass that sorts collected grid handles back-to-front and draws them
/// with alpha blending and depth writes disabled.
/// </summary>
internal sealed class OpenGlTransparentGeometryPass : RenderPass, ITransparentGeometryPass
{
    private readonly OpenGlRenderResources _resources;
    private readonly List<TransparentEntry> _sortedEntries = new();

    public OpenGlTransparentGeometryPass(OpenGlRenderResources resources)
    {
        _resources = resources ?? throw new ArgumentNullException(nameof(resources));
    }

    public override RenderPassPhase Phase => RenderPassPhase.Transparent;

    protected override void ExecuteCore(RenderFrameContext context)
    {
        if (!context.TryGet<OpenGlFrameQueues>(out OpenGlFrameQueues? queues) || queues is null || queues.Grids.Count == 0)
        {
            return;
        }

        _sortedEntries.Clear();
        for (int i = 0; i < queues.Grids.Count; i++)
        {
            OpenGlGridHandle handle = queues.Grids[i];
            float distanceSquared = Vector3.DistanceSquared(handle.Grid.GetActiveWorldPosition(), context.View.Position);
            _sortedEntries.Add(new TransparentEntry(handle, distanceSquared));
        }

        _sortedEntries.Sort(static (left, right) => right.DistanceSquared.CompareTo(left.DistanceSquared));

        bool cullFaceWasEnabled = _resources.Context.IsEnabled(EnableCap.CullFace);
        _resources.Context.SetAlphaBlend(true);
        _resources.Context.SetCullFace(false);
        _resources.Context.SetDepthMask(false);

        try
        {
            for (int i = 0; i < _sortedEntries.Count; i++)
            {
                _sortedEntries[i].Handle.DrawTransparent(_resources.LineShaderProgram, _resources.ViewportSize, context.View);
            }
        }
        finally
        {
            _resources.Context.SetDepthMask(true);
            _resources.Context.SetCullFace(cullFaceWasEnabled);
            _resources.Context.SetAlphaBlend(false);
        }
    }

    private readonly record struct TransparentEntry(OpenGlGridHandle Handle, float DistanceSquared);
}
