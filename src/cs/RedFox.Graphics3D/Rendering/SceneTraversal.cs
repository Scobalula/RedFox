using RedFox.Graphics3D.Rendering;
using RedFox.Graphics3D.Rendering.Materials;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace RedFox.Graphics3D.Rendering;

/// <summary>
/// Provides shared recursive traversal helpers for backend-driven rendering.
/// </summary>
public static class SceneTraversal
{
    internal static void CollectPostOrder(SceneNode node, List<SceneNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(nodes);

        if (node.Children is not null)
        {
            foreach (SceneNode child in node.Children)
            {
                CollectPostOrder(child, nodes);
            }
        }

        nodes.Add(node);
    }

    internal static void Update(
        IReadOnlyList<SceneNode> nodes,
        ICommandList commandList,
        IGraphicsDevice graphicsDevice,
        IMaterialTypeRegistry materialTypes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(commandList);
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(materialTypes);

        for (int i = 0; i < nodes.Count; i++)
        {
            SceneNode node = nodes[i];
            if ((node.Flags & SceneNodeFlags.NoDraw) != 0)
            {
                continue;
            }

            IRenderHandle? graphicsHandle = node.GraphicsHandle;
            if (graphicsHandle is null)
            {
                graphicsHandle = node.CreateRenderHandle(graphicsDevice, materialTypes);
                node.GraphicsHandle = graphicsHandle;
            }

            if (graphicsHandle?.RequiresPerFrameUpdate == true)
            {
                graphicsHandle.Update(commandList);
            }
        }
    }

    internal static void Render(
        IReadOnlyList<IRenderHandle> handles,
        ICommandList commandList,
        RenderPhase phase,
        in Matrix4x4 view,
        in Matrix4x4 projection,
        in Matrix4x4 sceneAxis,
        Vector3 cameraPosition,
        Vector2 viewportSize)
    {
        ArgumentNullException.ThrowIfNull(handles);
        ArgumentNullException.ThrowIfNull(commandList);

        RenderPhaseMask phaseMask = GetPhaseMask(phase);
        for (int i = 0; i < handles.Count; i++)
        {
            IRenderHandle handle = handles[i];
            if ((handle.RenderPhases & phaseMask) == 0)
            {
                continue;
            }

            handle.Render(commandList, phase, view, projection, sceneAxis, cameraPosition, viewportSize);
        }
    }

    internal static void Render(
        IReadOnlyList<SceneNode> nodes,
        ICommandList commandList,
        RenderPhase phase,
        in Matrix4x4 view,
        in Matrix4x4 projection,
        in Matrix4x4 sceneAxis,
        Vector3 cameraPosition,
        Vector2 viewportSize)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(commandList);

        RenderPhaseMask phaseMask = GetPhaseMask(phase);
        for (int i = 0; i < nodes.Count; i++)
        {
            SceneNode node = nodes[i];
            if ((node.Flags & SceneNodeFlags.NoDraw) != 0)
            {
                continue;
            }

            IRenderHandle? graphicsHandle = node.GraphicsHandle;
            if (graphicsHandle is null || (graphicsHandle.RenderPhases & phaseMask) == 0)
            {
                continue;
            }

            graphicsHandle.Render(commandList, phase, view, projection, sceneAxis, cameraPosition, viewportSize);
        }
    }

    /// <summary>
    /// Recursively updates render handles for a scene-node subtree in post-order.
    /// </summary>
    /// <param name="node">The root node to update.</param>
    /// <param name="commandList">The command list used for updates.</param>
    /// <param name="graphicsDevice">The graphics device that owns created resources.</param>
    /// <param name="materialTypes">The material-type registry used for handle creation.</param>
    public static void Update(SceneNode node, ICommandList commandList, IGraphicsDevice graphicsDevice, IMaterialTypeRegistry materialTypes)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(commandList);
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(materialTypes);

        if (node.Children is not null)
        {
            foreach (SceneNode child in node.Children)
            {
                Update(child, commandList, graphicsDevice, materialTypes);
            }
        }

        if ((node.Flags & SceneNodeFlags.NoDraw) != 0)
        {
            return;
        }

        IRenderHandle? graphicsHandle = node.GraphicsHandle;
        if (graphicsHandle is null)
        {
            graphicsHandle = node.CreateRenderHandle(graphicsDevice, materialTypes);
            node.GraphicsHandle = graphicsHandle;
        }

        if (graphicsHandle?.RequiresPerFrameUpdate == true)
        {
            graphicsHandle.Update(commandList);
        }
    }

    /// <summary>
    /// Recursively renders a scene-node subtree in post-order for the supplied phase.
    /// </summary>
    /// <param name="node">The root node to render.</param>
    /// <param name="commandList">The active command list.</param>
    /// <param name="phase">The render phase to execute.</param>
    /// <param name="view">The active view matrix.</param>
    /// <param name="projection">The active projection matrix.</param>
    /// <param name="sceneAxis">The scene-axis transform matrix.</param>
    /// <param name="cameraPosition">The active camera position.</param>
    /// <param name="viewportSize">The active viewport size in pixels.</param>
    public static void Render(
        SceneNode node,
        ICommandList commandList,
        RenderPhase phase,
        in Matrix4x4 view,
        in Matrix4x4 projection,
        in Matrix4x4 sceneAxis,
        Vector3 cameraPosition,
        Vector2 viewportSize)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(commandList);

        if (node.Children is not null)
        {
            foreach (SceneNode child in node.Children)
            {
                Render(child, commandList, phase, view, projection, sceneAxis, cameraPosition, viewportSize);
            }
        }

        if ((node.Flags & SceneNodeFlags.NoDraw) != 0)
        {
            return;
        }

        node.GraphicsHandle?.Render(commandList, phase, view, projection, sceneAxis, cameraPosition, viewportSize);
    }

    private static RenderPhaseMask GetPhaseMask(RenderPhase phase)
    {
        return phase switch
        {
            RenderPhase.SkinningCompute => RenderPhaseMask.SkinningCompute,
            RenderPhase.Opaque => RenderPhaseMask.Opaque,
            RenderPhase.Transparent => RenderPhaseMask.Transparent,
            RenderPhase.Overlay => RenderPhaseMask.Overlay,
            _ => RenderPhaseMask.None,
        };
    }
}