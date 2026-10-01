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
    private Matrix4x4[] _skinMatrixBuffer = [];
    private Matrix4x4[] _uploadedSkinMatrices = [];
    private float[] _uploadedMorphWeights = [];
    private Morph? _morphSource;

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


        VertexCount = Owner.Positions?.ElementCount ?? 0;
        IndexCount = Owner.FaceIndices?.TotalComponentCount ?? 0;

        if (_buffers.Count == 0)
        {
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
            _buffers.Add(MeshGpuBufferBinding.CreateShaderResource(Owner.Skin?.BoneIndices, "BoneIndexBuffer", BufferUsage.Sampled, normalizeIndexElementType: true));
            _buffers.Add(MeshGpuBufferBinding.CreateShaderResource(Owner.Skin?.BoneWeights, "BoneWeightBuffer", BufferUsage.Sampled, normalizeIndexElementType: false));
            _buffers.Add(MeshGpuBufferBinding.CreateShaderResource("SkinTransformBuffer", BufferUsage.Sampled | BufferUsage.DynamicWrite, normalizeIndexElementType: false, (binding, graphicsDevice) =>
            {
                if (Owner.Skin is not { } skin)
                {
                    binding.Release();
                    _skinMatrixBuffer = [];
                    _uploadedSkinMatrices = [];
                    return false;
                }

                scoped Span<Matrix4x4> matrixBuffer;
                if (skin.Bones.Count > 128)
                {
                    if (_skinMatrixBuffer.Length < skin.Bones.Count)
                        Array.Resize(ref _skinMatrixBuffer, skin.Bones.Count);

                    matrixBuffer = _skinMatrixBuffer.AsSpan(0, skin.Bones.Count);
                }
                else
                {
                    matrixBuffer = stackalloc Matrix4x4[skin.Bones.Count];
                }

                Owner.CopySkinTransforms(matrixBuffer);
                bool unchanged = binding.HasGpuBuffer && matrixBuffer.SequenceEqual(_uploadedSkinMatrices);
                if (unchanged)
                    return true;

                GpuBufferData transformData = new(MemoryMarshal.AsBytes(matrixBuffer), GpuBufferElementType.Float32, matrixBuffer.Length, 4, 4, 16 * sizeof(float), 4 * sizeof(float), sizeof(float));

                bool updated = binding.UpdateGenerated(graphicsDevice, transformData);
                if (updated)
                {
                    if (_uploadedSkinMatrices.Length != matrixBuffer.Length)
                        Array.Resize(ref _uploadedSkinMatrices, matrixBuffer.Length);

                    matrixBuffer.CopyTo(_uploadedSkinMatrices);
                }

                return updated;
            }));
            _buffers.Add(MeshGpuBufferBinding.CreateShaderResource(CreateMorphDeltaBuffer(Owner.Morph), "MorphDeltaBuffer", BufferUsage.Sampled, normalizeIndexElementType: false));
            _morphSource = Owner.Morph;
            _buffers.Add(MeshGpuBufferBinding.CreateShaderResource("MorphWeightBuffer", BufferUsage.Sampled | BufferUsage.DynamicWrite, normalizeIndexElementType: false, (binding, graphicsDevice) =>
            {
                if (Owner.Morph is not { TargetCount: > 0 } morph)
                {
                    binding.Release();
                    _uploadedMorphWeights = [];
                    return false;
                }

                bool unchanged = binding.HasGpuBuffer && morph.Weights.AsSpan().SequenceEqual(_uploadedMorphWeights);
                if (unchanged)
                    return true;

                GpuBufferData weightData = new(MemoryMarshal.AsBytes(morph.Weights.AsSpan()), GpuBufferElementType.Float32, morph.TargetCount, 1, 1, sizeof(float), sizeof(float), sizeof(float));
                bool updated = binding.UpdateGenerated(graphicsDevice, weightData);
                if (updated)
                {
                    if (_uploadedMorphWeights.Length != morph.TargetCount)
                        Array.Resize(ref _uploadedMorphWeights, morph.TargetCount);

                    morph.Weights.CopyTo(_uploadedMorphWeights, 0);
                }

                return updated;
            }));
        }

        if (VertexCount <= 0)
        {
            ReleaseBuffers();
            return;
        }

        _buffers[0].Update(_graphicsDevice, Owner.Positions);
        _buffers[1].Update(_graphicsDevice, Owner.Normals);
        _buffers[2].Update(_graphicsDevice, Owner.FaceIndices);
        _buffers[3].Update(_graphicsDevice, Owner.UVLayers);
        _buffers[4].Update(_graphicsDevice, Owner.Skin?.BoneIndices);
        _buffers[5].Update(_graphicsDevice, Owner.Skin?.BoneWeights);
        _buffers[6].Update(_graphicsDevice);
        if (!ReferenceEquals(_morphSource, Owner.Morph))
        {
            _morphSource = Owner.Morph;
            _buffers[7].Update(_graphicsDevice, CreateMorphDeltaBuffer(_morphSource));
        }
        else
        {
            _buffers[7].Update(_graphicsDevice);
        }
        _buffers[8].Update(_graphicsDevice);
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

        List<Material>? materials = Owner.Materials;
        if (materials is not { Count: > 0 })
        {
            return;
        }

        List<(int StartIndex, int IndexCount)>? materialRanges = Owner.MaterialIndexRanges;
        if (materialRanges is not null && (materialRanges.Count != materials.Count || IndexCount <= 0)
            || materialRanges is null && materials.Count > 1 && Owner.UVLayerCount < materials.Count)
        {
            throw new InvalidOperationException($"Mesh '{Owner.Name}' needs one index range per material or one UV layer per material.");
        }

        for (int i = 0; i < materials.Count; i++)
        {
            Material material = materials[i];

            MaterialRenderHandle materialHandle = SceneRenderResources.GetOrCreate(_graphicsDevice, material, () => new MaterialRenderHandle(_graphicsDevice, material));

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
            commandList.SetUniformInt("UVLayerIndex", materialRanges is null ? i : 0);
            commandList.SetUniformInt("SkinInfluenceCount", Owner.Skin is { Bones.Count: > 0 } skin ? skin.InfluenceCount : 0);
            commandList.SetUniformInt("SkinningMode", (int)(Owner.Skin?.SkinningMode == SkinningMode.DualQuaternion ? SkinningMode.DualQuaternion : commandList.SkinningMode));
            commandList.SetUniformInt("MorphTargetCount", Owner.Morph is { VertexCount: > 0 } morph ? morph.TargetCount : 0);

            foreach (MeshGpuBufferBinding buffer in _buffers)
            {
                buffer.Bind(commandList, pipeline);
            }

            if (IndexCount > 0)
            {
                (int startIndex, int indexCount) = materialRanges is not null
                    ? materialRanges[i]
                    : (0, IndexCount);
                if (startIndex < 0 || indexCount < 0 || startIndex > IndexCount - indexCount)
                {
                    throw new InvalidOperationException($"Mesh '{Owner.Name}' has an invalid material index range.");
                }

                commandList.DrawIndexed(indexCount, startIndex, 0);
            }
            else
            {
                commandList.Draw(VertexCount, 0);
            }
        }
    }

    /// <inheritdoc/>
    protected override void ReleaseResources()
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

        _skinMatrixBuffer = [];
        _uploadedSkinMatrices = [];
        _uploadedMorphWeights = [];
        _morphSource = null;
        VertexCount = 0;
        IndexCount = 0;
    }

    private static DataBuffer<float>? CreateMorphDeltaBuffer(Morph? morph)
    {
        if (morph is not { TargetCount: > 0, VertexCount: > 0 })
            return null;

        float[] deltas = new float[morph.VertexCount * morph.TargetCount * 8];

        for (int vertexIndex = 0; vertexIndex < morph.VertexCount; vertexIndex++)
        {
            for (int targetIndex = 0; targetIndex < morph.TargetCount; targetIndex++)
            {
                int offset = ((vertexIndex * morph.TargetCount) + targetIndex) * 8;
                Vector3 position = morph.DeltaPositions?.GetVector3(vertexIndex, targetIndex) ?? Vector3.Zero;
                Vector3 normal = morph.DeltaNormals?.GetVector3(vertexIndex, targetIndex) ?? Vector3.Zero;

                position.CopyTo(deltas, offset);
                normal.CopyTo(deltas, offset + 4);
            }
        }

        return new DataBuffer<float>(deltas, 2, 4);
    }
}
