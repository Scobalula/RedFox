using System;
using System.Numerics;
using System.Runtime.InteropServices;
using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.Rendering;
using RedFox.Graphics3D.Rendering.Materials;

namespace RedFox.Graphics3D.Rendering.Handles;

/// <summary>
/// Owns mesh GPU resources and coordinates material-driven rendering.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="MeshRenderHandle"/> class.
/// </remarks>
/// <param name="graphicsDevice">The graphics device that creates mesh resources.</param>
/// <param name="mesh">The mesh node represented by this handle.</param>
internal sealed class MeshRenderHandle(IGraphicsDevice graphicsDevice, Mesh mesh) : RenderHandle
{
    private readonly List<MeshGpuBufferBinding> _buffers = [];
    private readonly IGraphicsDevice _graphicsDevice = graphicsDevice;

    /// <summary>
    /// Gets the mesh that owns this handle.
    /// </summary>
    public Mesh Owner { get; } = mesh;

    /// <summary>
    /// Gets the number of vertices in the mesh.
    /// </summary>
    public int VertexCount { get; internal set; }

    /// <summary>
    /// Gets the number of indices in the mesh.
    /// </summary>
    public int IndexCount { get; internal set; }

    /// <inheritdoc/>
    public override bool RequiresPerFrameUpdate => true;

    /// <inheritdoc/>
    public override void Update(ICommandList commandList)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(commandList);


        if (_buffers.Count == 0)
        {
            VertexCount = Owner.Positions?.ElementCount ?? 0;
            IndexCount = Owner.FaceIndices?.TotalComponentCount ?? 0;

            if (VertexCount <= 0)
            {
                ReleaseBuffers();
                return;
            }

            if (Owner.Normals is not { ElementCount: > 0 })
            {
                Owner.GenerateNormals();
            }

            if (Owner.Normals is not { ElementCount: > 0 })
            {
                ReleaseBuffers();
                return;
            }

            _buffers.Add(MeshGpuBufferBinding.CreateVertex(Owner.Positions, nameof(Owner.Positions)));
            _buffers.Add(MeshGpuBufferBinding.CreateVertex(Owner.Normals, nameof(Owner.Normals)));
            _buffers.Add(MeshGpuBufferBinding.CreateIndex(Owner.FaceIndices, nameof(Owner.FaceIndices)));
            _buffers.Add(MeshGpuBufferBinding.CreateShaderResource(Owner.UVLayers, "UVLayerBuffer", BufferUsage.Sampled, normalizeIndexElementType: false));
            _buffers.Add(MeshGpuBufferBinding.CreateShaderResource(Owner.BoneIndices, "BoneIndexBuffer", BufferUsage.Sampled, normalizeIndexElementType: true));
            _buffers.Add(MeshGpuBufferBinding.CreateShaderResource(Owner.BoneWeights, "BoneWeightBuffer", BufferUsage.Sampled, normalizeIndexElementType: false));
            _buffers.Add(MeshGpuBufferBinding.CreateShaderResource("SkinTransformBuffer", BufferUsage.Sampled | BufferUsage.DynamicWrite, normalizeIndexElementType: false, (binding, graphicsDevice) =>
            {
                if (Owner.BoneIndices is null || Owner.BoneWeights is null || Owner.SkinnedBones is null)
                {
                    binding.Release();
                    return false;
                }

                Span<Matrix4x4> matrixBuffer = Owner.SkinnedBones.Count > 128 ? new Matrix4x4[Owner.SkinnedBones.Count] : stackalloc Matrix4x4[Owner.SkinnedBones.Count];
                Owner.CopySkinTransforms(matrixBuffer);

                GpuBufferData transformData = new(
                    MemoryMarshal.AsBytes(matrixBuffer),
                    GpuBufferElementType.Float32,
                    matrixBuffer.Length,
                    4,
                    4,
                    16 * sizeof(float),
                    4 * sizeof(float),
                    sizeof(float));

                bool updated = binding.UpdateGenerated(graphicsDevice, transformData);

                return updated;
            }));
        }

        if (VertexCount <= 0)
        {
            ReleaseBuffers();
            return;
        }

        foreach (MeshGpuBufferBinding buffer in _buffers)
        {
            buffer.Update(_graphicsDevice);
        }
    }

    /// <inheritdoc/>
    public override void Render(ICommandList commandList, RenderFlags phase, in Matrix4x4 view, in Matrix4x4 projection, in Matrix4x4 sceneAxis, Vector3 cameraPosition, Vector2 viewportSize)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(commandList);

        if (phase != Rendering.RenderFlags.Opaque || VertexCount <= 0)
        {
            return;
        }

        if (Owner.Materials is not { Count: > 0 })
        {
            return;
        }

        for (int i = 0; i < Owner.Materials.Count; i++)
        {
            Material material = Owner.Materials[i];

            if (material.GraphicsHandle is not MaterialRenderHandle materialHandle)
            {
                continue;
            }

            materialHandle.BindResources(commandList);

            if (materialHandle.Pipeline is not { } pipeline)
            {
                continue;
            }

            commandList.SetUniformMatrix4x4("Model", Owner.GetBindWorldMatrix());
            commandList.SetUniformMatrix4x4("View", view);
            commandList.SetUniformMatrix4x4("Projection", projection);
            commandList.SetUniformMatrix4x4("SceneAxis", sceneAxis);
            commandList.SetUniformVector3("CameraPosition", cameraPosition);
            commandList.SetUniformInt("UVLayerCount", Owner.UVLayerCount);
            commandList.SetUniformInt("UVLayerIndex", 0);
            commandList.SetUniformInt("SkinInfluenceCount", Owner.BoneIndices is null ? 0 : Owner.BoneIndices.ValueCount);

            foreach (MeshGpuBufferBinding buffer in _buffers)
            {
                buffer.Bind(commandList, pipeline);
            }

            if (IndexCount > 0)
            {
                commandList.DrawIndexed(IndexCount, 0, 0);
            }
            else
            {
                commandList.Draw(VertexCount, 0);
            }
        }
    }

    /// <inheritdoc/>
    protected override void ReleaseCore()
    {
        ReleaseBuffers();
    }

    private void ReleaseBuffers()
    {
        if (_buffers.Count != 0)
        {
            foreach (MeshGpuBufferBinding buffer in _buffers)
            {
                buffer.Release();
            }

            _buffers.Clear();
        }

        VertexCount = 0;
        IndexCount = 0;
    }
}
