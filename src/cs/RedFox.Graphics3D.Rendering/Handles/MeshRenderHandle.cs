using System;
using System.Diagnostics.CodeAnalysis;
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
    private MaterialRenderHandle[] _materialHandles = [];
    private Matrix4x4[] _skinMatrixBuffer = [];
    private Matrix4x4[] _uploadedSkinMatrices = [];
    private float[] _uploadedMorphWeights = [];
    private Morph? _morphSource;
    private bool _isReadyToRender = true;
    private DataBuffer? _boundsSource;
    private SceneBounds _localBounds;
    private MeshGpuBufferBinding? _wireframeIndices;
    private DataBuffer? _wireframeFaceIndices;
    private int _wireframeVertexCount;

    internal int GeometryVersion { get; private set; }

    internal bool HasOpaqueMaterials { get; private set; }

    internal bool HasTransparentMaterials { get; private set; }

    internal float ViewDistanceSquared { get; private set; }

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

    public override bool HasPendingUploads => !_isReadyToRender;

    private DataBuffer? SampledTangents => Owner.Tangents is DataBuffer<float> { ValueCount: 1, ComponentCount: 4 } ? Owner.Tangents : null;

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
                    GeometryVersion++;
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
                    GeometryVersion++;
                    if (_uploadedMorphWeights.Length != morph.TargetCount)
                        Array.Resize(ref _uploadedMorphWeights, morph.TargetCount);

                    morph.Weights.CopyTo(_uploadedMorphWeights, 0);
                }

                return updated;
            }));
            _buffers.Add(MeshGpuBufferBinding.CreateShaderResource(SampledTangents, "TangentBuffer", BufferUsage.Sampled, normalizeIndexElementType: false));
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
            GeometryVersion++;
            _morphSource = Owner.Morph;
            _buffers[7].Update(_graphicsDevice, CreateMorphDeltaBuffer(_morphSource));
        }
        else
        {
            _buffers[7].Update(_graphicsDevice);
        }
        _buffers[8].Update(_graphicsDevice);
        _buffers[9].Update(_graphicsDevice, SampledTangents);
        _isReadyToRender = true;
        foreach (MeshGpuBufferBinding buffer in _buffers)
        {
            if (buffer.HasPendingUpload)
            {
                _isReadyToRender = false;
                break;
            }
        }

        UpdateMaterialHandles(commandList);
    }

    /// <inheritdoc/>
    public override void Render(ICommandList commandList, RenderFlags phase, in Matrix4x4 view, in Matrix4x4 projection, in Matrix4x4 sceneAxis, Vector3 cameraPosition, Vector2 viewportSize)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(commandList);

        if ((phase != Rendering.RenderFlags.Opaque && phase != Rendering.RenderFlags.Transparent && phase != Rendering.RenderFlags.Shadow && phase != Rendering.RenderFlags.DepthPrepass) || VertexCount <= 0 || !_isReadyToRender)
        {
            return;
        }

        List<Material>? materials = Owner.Materials;
        if (materials is not { Count: > 0 } || materials.Count != _materialHandles.Length)
        {
            return;
        }

        List<(int StartIndex, int IndexCount)>? materialRanges = Owner.MaterialIndexRanges;
        if (materialRanges is not null && (materialRanges.Count != materials.Count || IndexCount <= 0)
            || materialRanges is null && materials.Count > 1 && Owner.UVLayerCount < materials.Count)
        {
            throw new InvalidOperationException($"Mesh '{Owner.Name}' needs one index range per material or one UV layer per material.");
        }

        Matrix4x4	model			= Owner.GetBindWorldMatrix();
        Matrix4x4	normalMatrix	= CreateNormalMatrix(model * sceneAxis);

        for (int i = 0; i < materials.Count; i++)
        {
            MaterialRenderHandle materialHandle = _materialHandles[i];

            if ((phase == Rendering.RenderFlags.DepthPrepass || phase == Rendering.RenderFlags.Opaque) && !materialHandle.IsOpaque || phase == Rendering.RenderFlags.Transparent && materialHandle.IsOpaque)
            {
                continue;
            }

            materialHandle.BindResources(commandList);

            if (materialHandle.Pipeline is not { } pipeline)
            {
                continue;
            }

            BindDrawState(commandList, pipeline, model, normalMatrix, view, projection, sceneAxis, cameraPosition, materialRanges is null ? i : 0, phase == Rendering.RenderFlags.DepthPrepass);

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

    internal void RenderWireframe(ICommandList commandList, IGpuPipelineState pipeline, float depthBias, in Matrix4x4 view, in Matrix4x4 projection, in Matrix4x4 sceneAxis, Vector3 cameraPosition)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(commandList);

        if (VertexCount <= 0 || !_isReadyToRender || !UpdateWireframeIndices())
        {
            return;
        }

        Matrix4x4	model			= Owner.GetBindWorldMatrix();
        Matrix4x4	normalMatrix	= CreateNormalMatrix(model * sceneAxis);

        commandList.SetPipelineState(pipeline);
        BindDrawState(commandList, pipeline, model, normalMatrix, view, projection, sceneAxis, cameraPosition, 0, false);
        commandList.SetUniformFloat("ClipDepthBias", depthBias);
        _wireframeIndices.Bind(commandList, pipeline);
        commandList.DrawIndexed(_wireframeIndices.TotalComponentCount, 0, 0);
    }

    internal bool UpdateVisibility(in ViewFrustum frustum, in Matrix4x4 sceneAxis, Vector3 cameraPosition)
    {
        SceneBounds bounds = GetWorldBounds(sceneAxis);
        ViewDistanceSquared = Vector3.DistanceSquared(bounds.Center, cameraPosition);
        return frustum.Intersects(bounds);
    }

    internal SceneBounds GetWorldBounds(in Matrix4x4 sceneAxis)
    {
        if (Owner.Skin is not null && Owner.TryGetSceneBounds(out SceneBounds skinnedBounds))
        {
            return skinnedBounds.Transform(sceneAxis);
        }

        if (!ReferenceEquals(_boundsSource, Owner.Positions))
        {
            _localBounds = SceneBounds.Invalid;
            for (int i = 0; i < VertexCount; i++)
            {
                _localBounds = _localBounds.Include(Owner.Positions!.GetVector3(i, 0));
            }

            _boundsSource = Owner.Positions;
        }

        return _localBounds.Expand(Owner.Morph?.GetMaxPositionOffset() ?? 0.0f).Transform(Owner.GetBindWorldMatrix() * sceneAxis);
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

        _wireframeIndices?.Release();
        _wireframeIndices = null;
        _wireframeFaceIndices = null;
        _wireframeVertexCount = 0;
        _skinMatrixBuffer = [];
        _uploadedSkinMatrices = [];
        _uploadedMorphWeights = [];
        _morphSource = null;
        _materialHandles = [];
        _isReadyToRender = true;
        _boundsSource = null;
        VertexCount = 0;
        IndexCount = 0;
    }

    private void UpdateMaterialHandles(ICommandList commandList)
    {
        HasOpaqueMaterials		= false;
        HasTransparentMaterials	= false;

        if (Owner.Materials is not { } materials)
        {
            _materialHandles = [];
            return;
        }

        if (_materialHandles.Length != materials.Count)
        {
            _materialHandles = new MaterialRenderHandle[materials.Count];
        }

        for (int i = 0; i < materials.Count; i++)
        {
            MaterialRenderHandle? materialHandle = _materialHandles[i];
            if (materialHandle is null || materialHandle.IsReleased || !ReferenceEquals(materialHandle.Material, materials[i]))
            {
                Material material = materials[i];
                materialHandle = SceneRenderResources.GetOrCreate(_graphicsDevice, material, () => new MaterialRenderHandle(_graphicsDevice, material));
                _materialHandles[i] = materialHandle;
            }

            materialHandle.Update(commandList);
            HasOpaqueMaterials		|= materialHandle.IsOpaque;
            HasTransparentMaterials	|= !materialHandle.IsOpaque;
        }
    }

    private void BindDrawState(ICommandList commandList, IGpuPipelineState pipeline, in Matrix4x4 model, in Matrix4x4 normalMatrix, in Matrix4x4 view, in Matrix4x4 projection, in Matrix4x4 sceneAxis, Vector3 cameraPosition, int uvLayerIndex, bool depthPrepass)
    {
        commandList.SetUniformMatrix4x4("Model", model);
        commandList.SetUniformMatrix4x4("NormalMatrix", normalMatrix);
        commandList.SetUniformMatrix4x4("View", view);
        commandList.SetUniformMatrix4x4("Projection", projection);
        commandList.SetUniformMatrix4x4("SceneAxis", sceneAxis);
        commandList.SetUniformVector3("CameraPosition", cameraPosition);
        commandList.SetUniformInt("UVLayerCount", Owner.UVLayerCount);
        commandList.SetUniformInt("UVLayerIndex", uvLayerIndex);
        commandList.SetUniformInt("SkinInfluenceCount", Owner.Skin is { Bones.Count: > 0 } skin ? skin.InfluenceCount : 0);
        commandList.SetUniformInt("SkinningMode", (int)(Owner.Skin?.SkinningMode == SkinningMode.DualQuaternion ? SkinningMode.DualQuaternion : commandList.SkinningMode));
        commandList.SetUniformInt("MorphTargetCount", Owner.Morph is { VertexCount: > 0 } morph ? morph.TargetCount : 0);
        commandList.SetUniformInt("HasTangents", _buffers[9].HasGpuBuffer ? 1 : 0);
        commandList.SetUniformInt("DepthPrepass", depthPrepass ? 1 : 0);

        foreach (MeshGpuBufferBinding buffer in _buffers)
        {
            buffer.Bind(commandList, pipeline);
        }
    }

    [MemberNotNullWhen(true, nameof(_wireframeIndices))]
    private bool UpdateWireframeIndices()
    {
        if (_wireframeIndices is null || !ReferenceEquals(_wireframeFaceIndices, Owner.FaceIndices) || _wireframeVertexCount != VertexCount)
        {
            _wireframeIndices?.Release();
            _wireframeIndices		= MeshGpuBufferBinding.CreateIndex(CreateWireframeIndices(), "WireframeIndices");
            _wireframeFaceIndices	= Owner.FaceIndices;
            _wireframeVertexCount	= VertexCount;
        }

        return _wireframeIndices.Update(_graphicsDevice);
    }

    private DataBuffer<uint> CreateWireframeIndices()
    {
        uint[] faceIndices = Owner.FaceIndices is { TotalComponentCount: > 0 } faces ? faces.ToArray<uint>() : [.. Enumerable.Range(0, VertexCount).Select(index => (uint)index)];
        uint[] lineIndices = new uint[faceIndices.Length / 3 * 6];

        for (int face = 0; face < lineIndices.Length / 6; face++)
        {
            ReadOnlySpan<uint>	triangle	= faceIndices.AsSpan(face * 3, 3);
            Span<uint>			edges		= lineIndices.AsSpan(face * 6, 6);
            edges[0] = triangle[0];
            edges[1] = triangle[1];
            edges[2] = triangle[1];
            edges[3] = triangle[2];
            edges[4] = triangle[2];
            edges[5] = triangle[0];
        }

        return new DataBuffer<uint>(lineIndices, 1, 2);
    }

    private static Matrix4x4 CreateNormalMatrix(in Matrix4x4 world)
    {
        return Matrix4x4.Invert(world, out Matrix4x4 inverseWorld) ? Matrix4x4.Transpose(inverseWorld) : world;
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
