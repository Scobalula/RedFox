using System;
using System.Numerics;
using RedFox.Graphics3D.Rendering.Backend;

namespace RedFox.Graphics3D.Rendering.Handles;

/// <summary>
/// Stores skeleton-bone render state for overlay rendering.
/// </summary>
internal sealed class SkeletonBoneRenderHandle : RenderHandle
{
    private readonly SkeletonBone _bone;

    private Matrix4x4 _worldMatrix;

    /// <summary>
    /// Initializes a new instance of the <see cref="SkeletonBoneRenderHandle"/> class.
    /// </summary>
    /// <param name="bone">The skeleton bone node represented by this handle.</param>
    public SkeletonBoneRenderHandle(SkeletonBone bone)
    {
        _bone = bone ?? throw new ArgumentNullException(nameof(bone));
    }

    /// <inheritdoc/>
    public override void Update(ICommandList commandList)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(commandList);
        _worldMatrix = _bone.GetActiveWorldMatrix();
    }

    /// <inheritdoc/>
    public override void Render(
        ICommandList commandList,
        RenderPhase phase,
        in Matrix4x4 view,
        in Matrix4x4 projection,
        in Matrix4x4 sceneAxis,
        Vector3 cameraPosition,
        Vector2 viewportSize)
    {
        ThrowIfDisposed();

        if (phase != RenderPhase.Overlay)
        {
            return;
        }

        commandList.SetUniformMatrix4x4("uModel", _worldMatrix);
    }
}