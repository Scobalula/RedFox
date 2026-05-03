using RedFox.Graphics3D.Rendering;
using RedFox.Graphics3D.Rendering.Materials;
using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace RedFox.Graphics3D.Rendering.Handles;

/// <summary>
/// Owns mesh GPU resources and coordinates material-driven rendering.
/// </summary>
internal sealed class MeshRenderHandle : RenderHandle
{
    private const int DrawPositionBufferSlot = 0;
    private const int DrawNormalBufferSlot = 1;
    private const int DrawBoneIndexBufferSlot = 12;
    private const int DrawBoneWeightBufferSlot = 13;
    private const int DrawSkinTransformBufferSlot = 14;
    private const int IndexBufferSlot = 8;

    private readonly IGraphicsDevice _graphicsDevice;
    private readonly Mesh _mesh;

    private Buffers.DataBuffer? _cachedBoneIndices;
    private Buffers.DataBuffer? _cachedBoneWeights;
    private Buffers.DataBuffer? _cachedFaceIndices;
    private Buffers.DataBuffer? _cachedNormals;
    private Buffers.DataBuffer? _cachedPositions;
    private bool _cachedUsesComputeSkinning;
    private IGpuBuffer? _boneIndexBuffer;
    private int _boneIndexBufferSizeBytes;
    private int _boneCount;
    private IGpuBuffer? _boneWeightBuffer;
    private int _boneWeightBufferSizeBytes;
    private IGpuPipelineState? _defaultPipeline;
    private int _indexBufferSizeBytes;
    private int _indexCount;
    private IGpuBuffer? _indexBuffer;
    private IGpuBuffer? _normalBuffer;
    private int _normalBufferSizeBytes;
    private IGpuBuffer? _positionBuffer;
    private int _positionBufferSizeBytes;
    private RenderPhaseMask _renderPhases;
    private int _skinInfluenceCount;
    private IGpuBuffer? _skinTransformBuffer;
    private int _skinTransformBufferSizeBytes;
    private Matrix4x4[] _lastSkinTransforms = Array.Empty<Matrix4x4>();
    private int _lastSkinTransformCount;
    private Matrix4x4[] _skinTransformsScratch = Array.Empty<Matrix4x4>();
    private int _vertexCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="MeshRenderHandle"/> class.
    /// </summary>
    /// <param name="graphicsDevice">The graphics device that creates mesh resources.</param>
    /// <param name="mesh">The mesh node represented by this handle.</param>
    public MeshRenderHandle(IGraphicsDevice graphicsDevice, Mesh mesh)
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        _mesh = mesh ?? throw new ArgumentNullException(nameof(mesh));
    }

    /// <inheritdoc/>
    public override RenderPhaseMask RenderPhases => _renderPhases;

    /// <inheritdoc/>
    public override bool RequiresPerFrameUpdate => NeedsPerFrameUpdate();

    /// <inheritdoc/>
    public override void Update(ICommandList commandList)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(commandList);

        _renderPhases = RenderPhaseMask.None;

        if (_mesh.Positions is not { ElementCount: > 0 } positions)
        {
            ReleaseBuffers();
            return;
        }

        if (_mesh.Normals is not { ElementCount: > 0 })
        {
            _mesh.GenerateNormals();
        }

        if (_mesh.Normals is not { ElementCount: > 0 } normals)
        {
            ReleaseBuffers();
            return;
        }

        _vertexCount = positions.ElementCount;
        bool vertexSkinningRequested = IsVertexSkinningRequested();

        if (!vertexSkinningRequested && CanSkipStaticGeometryUpdate(positions, normals, _mesh.FaceIndices))
        {
            _renderPhases = RenderPhaseMask.Opaque;
            return;
        }

        bool geometryChanged = GeometryChanged(positions, normals, _mesh.FaceIndices);

        if (vertexSkinningRequested)
        {
            bool skinningInputsChanged = geometryChanged
                || !_cachedUsesComputeSkinning
                || !ReferenceEquals(_cachedBoneIndices, _mesh.BoneIndices)
                || !ReferenceEquals(_cachedBoneWeights, _mesh.BoneWeights)
                || _positionBuffer is null
                || _normalBuffer is null
                || _boneIndexBuffer is null
                || _boneWeightBuffer is null;

            if (skinningInputsChanged)
            {
                GpuBufferData positionData = GetRequiredGpuBufferData(positions, nameof(_mesh.Positions));
                GpuBufferData normalData = GetRequiredGpuBufferData(normals, nameof(_mesh.Normals));
                if (!TryBuildSkinningData(_mesh, _vertexCount, out GpuBufferData skinIndexData, out GpuBufferData skinWeightData, out int influenceCount, out int boneCount))
                {
                    ReleaseComputeBuffers();
                    vertexSkinningRequested = false;
                }
                else
                {
                    skinIndexData = NormalizeIndexElementType(skinIndexData);
                    _skinInfluenceCount = influenceCount;
                    _boneCount = boneCount;
                    EnsureOrUpdateBuffer(ref _positionBuffer, ref _positionBufferSizeBytes, positionData.ElementStrideBytes, BufferUsage.Vertex, positionData);
                    EnsureOrUpdateBuffer(ref _normalBuffer, ref _normalBufferSizeBytes, normalData.ElementStrideBytes, BufferUsage.Vertex, normalData);
                    EnsureOrUpdateBuffer(ref _boneIndexBuffer, ref _boneIndexBufferSizeBytes, skinIndexData.ValueStrideBytes, BufferUsage.Sampled, skinIndexData);
                    EnsureOrUpdateBuffer(ref _boneWeightBuffer, ref _boneWeightBufferSizeBytes, skinWeightData.ValueStrideBytes, BufferUsage.Sampled, skinWeightData);
                    _cachedBoneIndices = _mesh.BoneIndices;
                    _cachedBoneWeights = _mesh.BoneWeights;
                    _cachedUsesComputeSkinning = true;
                }
            }

            if (vertexSkinningRequested)
            {
                UpdateSkinTransformBuffer();
                vertexSkinningRequested = HasVertexSkinningResources();
                _cachedPositions = positions;
                _cachedNormals = normals;
                _cachedFaceIndices = _mesh.FaceIndices;
                _cachedUsesComputeSkinning = vertexSkinningRequested;
            }
        }

        if (!vertexSkinningRequested)
        {
            bool cachedUsedSkinning = _cachedUsesComputeSkinning;
            _skinInfluenceCount = 0;
            _boneCount = 0;
            ReleaseComputeBuffers();

            if (geometryChanged || cachedUsedSkinning || _positionBuffer is null || _normalBuffer is null)
            {
                GpuBufferData positionData = GetRequiredGpuBufferData(positions, nameof(_mesh.Positions));
                GpuBufferData normalData = GetRequiredGpuBufferData(normals, nameof(_mesh.Normals));
                EnsureOrUpdateBuffer(ref _positionBuffer, ref _positionBufferSizeBytes, positionData.ElementStrideBytes, BufferUsage.Vertex, positionData);
                EnsureOrUpdateBuffer(ref _normalBuffer, ref _normalBufferSizeBytes, normalData.ElementStrideBytes, BufferUsage.Vertex, normalData);
                _cachedPositions = positions;
                _cachedNormals = normals;
            }

            _cachedBoneIndices = null;
            _cachedBoneWeights = null;
            _cachedUsesComputeSkinning = false;
        }



        if (geometryChanged || _indexBuffer is null != (_mesh.FaceIndices is not { ElementCount: > 0 }))
        {
            if (_mesh.FaceIndices is { ElementCount: > 0 } faceIndices)
            {
                GpuBufferData indexData = NormalizeIndexElementType(GetRequiredGpuBufferData(faceIndices, nameof(_mesh.FaceIndices)));
                EnsureOrUpdateBuffer(ref _indexBuffer, ref _indexBufferSizeBytes, indexData.ComponentSizeBytes, BufferUsage.Index, indexData);
                _indexCount = indexData.TotalComponentCount;
            }
            else
            {
                DisposeBuffer(ref _indexBuffer, ref _indexBufferSizeBytes);
                _indexCount = 0;
            }

            _cachedFaceIndices = _mesh.FaceIndices;
        }


        if (_vertexCount > 0 && _positionBuffer is not null && _normalBuffer is not null)
        {
            _renderPhases = RenderPhaseMask.Opaque;
        }
    }

    /// <inheritdoc/>
    public override void Render(ICommandList commandList, RenderPhase phase, in Matrix4x4 view, in Matrix4x4 projection, in Matrix4x4 sceneAxis, Vector3 cameraPosition, Vector2 viewportSize)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(commandList);

        if (phase != RenderPhase.Opaque || _vertexCount <= 0 || _positionBuffer is null || _normalBuffer is null)
        {
            return;
        }

        if (_indexBuffer is null || _indexCount <= 0)
        {
            return;
        }

        if (_mesh.Materials is not { Count: > 0 })
        {
            return;
        }

        for (int i = 0; i < _mesh.Materials.Count; i++)
        {
            Material material = _mesh.Materials[0];

            if (material.GraphicsHandle is null)
                continue;

            material.GraphicsHandle.Render(commandList, phase, view, projection, sceneAxis, cameraPosition, viewportSize);

            bool useVertexSkinning = HasVertexSkinningResources();

            commandList.SetUniformMatrix4x4("Model", _mesh.GetBindWorldMatrix());
            commandList.SetUniformMatrix4x4("View", view);
            commandList.SetUniformMatrix4x4("Projection", projection);
            commandList.SetUniformMatrix4x4("SceneAxis", sceneAxis);
            commandList.SetUniformVector3("CameraPosition", cameraPosition);
            commandList.SetUniformInt("SkinInfluenceCount", useVertexSkinning ? _skinInfluenceCount : 0);
            commandList.SetUniformInt("BoneIndexBuffer", DrawBoneIndexBufferSlot);
            commandList.SetUniformInt("BoneWeightBuffer", DrawBoneWeightBufferSlot);
            commandList.SetUniformInt("SkinTransformBuffer", DrawSkinTransformBufferSlot);

            commandList.BindBuffer(DrawPositionBufferSlot, _positionBuffer);
            commandList.BindBuffer(DrawNormalBufferSlot, _normalBuffer);

            if (useVertexSkinning)
            {
                commandList.BindBuffer(DrawBoneIndexBufferSlot, _boneIndexBuffer!);
                commandList.BindBuffer(DrawBoneWeightBufferSlot, _boneWeightBuffer!);
                commandList.BindBuffer(DrawSkinTransformBufferSlot, _skinTransformBuffer!);
            }

            if (_indexBuffer is not null && _indexCount > 0)
            {
                commandList.BindBuffer(IndexBufferSlot, _indexBuffer);
                commandList.DrawIndexed(_indexCount, 0, 0);
                return;
            }
            else
            {
                commandList.Draw(_vertexCount, 0);
            }
        }
    }

    /// <inheritdoc/>
    protected override void ReleaseCore()
    {
        _defaultPipeline?.Dispose();
        _defaultPipeline = null;
        ReleaseBuffers();
    }

    private void EnsureOrUpdateBuffer(ref IGpuBuffer? buffer, ref int sizeBytes, int stride, BufferUsage usage, GpuBufferData data)
    {
        if (data.Bytes.IsEmpty || stride <= 0)
        {
            DisposeBuffer(ref buffer, ref sizeBytes);
            return;
        }

        if (buffer is null
            || buffer.IsDisposed
            || sizeBytes != data.SizeBytes
            || buffer.SizeBytes != data.SizeBytes
            || buffer.StrideBytes != stride
            || buffer.Usage != usage
            || buffer.ElementType != data.ElementType)
        {
            buffer?.Dispose();
            buffer = _graphicsDevice.CreateBuffer(data.SizeBytes, stride, usage, data.ElementType, data.Bytes);
            sizeBytes = data.SizeBytes;
            return;
        }

        _graphicsDevice.UpdateBuffer(buffer, data.Bytes);
    }


    private bool GeometryChanged(Buffers.DataBuffer positions, Buffers.DataBuffer normals, Buffers.DataBuffer? faceIndices)
    {
        return !ReferenceEquals(_cachedPositions, positions)
            || !ReferenceEquals(_cachedNormals, normals)
            || !ReferenceEquals(_cachedFaceIndices, faceIndices)
            || _positionBuffer is null
            || _normalBuffer is null;
    }

    private void EnsurePipeline(ref IGpuPipelineState? pipeline, string typeName)
    {
        if (pipeline is not null)
        {
            return;
        }

        if (_graphicsDevice.MaterialTypes is not IMaterialPipelineProvider pipelineProvider)
        {
            throw new InvalidOperationException($"Material registry '{_graphicsDevice.MaterialTypes.GetType().Name}' does not provide runtime pipeline services.");
        }

        pipeline = pipelineProvider.CreatePipeline(_graphicsDevice, typeName);
    }

    private bool UpdateSkinTransformBuffer()
    {
        if (_boneCount <= 0)
        {
            DisposeBuffer(ref _skinTransformBuffer, ref _skinTransformBufferSizeBytes);
            _lastSkinTransformCount = 0;
            return false;
        }

        EnsureSkinTransformsScratchCapacity(_boneCount);
        int copiedTransforms = _mesh.CopySkinTransforms(_skinTransformsScratch);
        if (copiedTransforms < _boneCount)
        {
            DisposeBuffer(ref _skinTransformBuffer, ref _skinTransformBufferSizeBytes);
            _lastSkinTransformCount = 0;
            return false;
        }

        ReadOnlySpan<Matrix4x4> transforms = new(_skinTransformsScratch, 0, copiedTransforms);
        bool transformsChanged = SkinTransformsChanged(transforms);
        if (!transformsChanged && _skinTransformBuffer is not null)
        {
            return false;
        }

        GpuBufferData transformData = new(
            MemoryMarshal.AsBytes(transforms),
            GpuBufferElementType.Float32,
            copiedTransforms,
            4,
            4,
            16 * sizeof(float),
            4 * sizeof(float),
            sizeof(float));
        EnsureOrUpdateBuffer(ref _skinTransformBuffer, ref _skinTransformBufferSizeBytes, transformData.ValueStrideBytes, BufferUsage.Sampled | BufferUsage.DynamicWrite, transformData);
        CacheSkinTransforms(transforms);
        return true;
    }

    private bool SkinTransformsChanged(ReadOnlySpan<Matrix4x4> transforms)
    {
        if (_lastSkinTransformCount != transforms.Length || _lastSkinTransforms.Length < transforms.Length)
        {
            return true;
        }

        ReadOnlySpan<Matrix4x4> lastTransforms = new(_lastSkinTransforms, 0, transforms.Length);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (lastTransforms[i] != transforms[i])
            {
                return true;
            }
        }

        return false;
    }

    private void CacheSkinTransforms(ReadOnlySpan<Matrix4x4> transforms)
    {
        if (_lastSkinTransforms.Length < transforms.Length)
        {
            _lastSkinTransforms = new Matrix4x4[transforms.Length];
        }

        transforms.CopyTo(_lastSkinTransforms);
        _lastSkinTransformCount = transforms.Length;
    }

    private void EnsureSkinTransformsScratchCapacity(int requiredBoneCount)
    {
        if (requiredBoneCount <= 0)
        {
            return;
        }

        if (_skinTransformsScratch.Length < requiredBoneCount)
        {
            _skinTransformsScratch = new Matrix4x4[requiredBoneCount];
        }
    }

    private void ReleaseComputeBuffers()
    {
        DisposeBuffer(ref _boneIndexBuffer, ref _boneIndexBufferSizeBytes);
        DisposeBuffer(ref _boneWeightBuffer, ref _boneWeightBufferSizeBytes);
        DisposeBuffer(ref _skinTransformBuffer, ref _skinTransformBufferSizeBytes);
        _lastSkinTransformCount = 0;
    }

    private void DisposeBuffer(ref IGpuBuffer? buffer, ref int sizeBytes)
    {
        buffer?.Dispose();
        buffer = null;
        sizeBytes = 0;
    }

    private void ReleaseBuffers()
    {
        ReleaseComputeBuffers();
        DisposeBuffer(ref _positionBuffer, ref _positionBufferSizeBytes);
        DisposeBuffer(ref _normalBuffer, ref _normalBufferSizeBytes);
        DisposeBuffer(ref _indexBuffer, ref _indexBufferSizeBytes);
        _cachedPositions = null;
        _cachedNormals = null;
        _cachedFaceIndices = null;
        _cachedBoneIndices = null;
        _cachedBoneWeights = null;
        _cachedUsesComputeSkinning = false;
        _vertexCount = 0;
        _indexCount = 0;
        _skinInfluenceCount = 0;
        _boneCount = 0;
        _lastSkinTransformCount = 0;
        _renderPhases = RenderPhaseMask.None;
    }

    private bool NeedsPerFrameUpdate()
    {
        if (IsVertexSkinningRequested())
        {
            return true;
        }

        if (_mesh.Positions is not { ElementCount: > 0 } positions)
        {
            return true;
        }

        if (_mesh.Normals is not { ElementCount: > 0 } normals)
        {
            return true;
        }

        return !CanSkipStaticGeometryUpdate(positions, normals, _mesh.FaceIndices);
    }

    private bool CanSkipStaticGeometryUpdate(Buffers.DataBuffer positions, Buffers.DataBuffer normals, Buffers.DataBuffer? faceIndices)
    {
        return !_cachedUsesComputeSkinning
            && _positionBuffer is not null
            && _normalBuffer is not null
            && ReferenceEquals(_cachedPositions, positions)
            && ReferenceEquals(_cachedNormals, normals)
            && ReferenceEquals(_cachedFaceIndices, faceIndices);
    }

    private bool IsVertexSkinningRequested()
    {
        return _mesh.HasSkinning
            && _mesh.BoneIndices is { ElementCount: > 0 }
            && _mesh.BoneWeights is { ElementCount: > 0 }
            && _mesh.SkinnedBones is { Count: > 0 };
    }

    private bool HasVertexSkinningResources()
    {
        return _skinInfluenceCount > 0
            && _boneIndexBuffer is not null
            && _boneWeightBuffer is not null
            && _skinTransformBuffer is not null;
    }

    private static GpuBufferData GetRequiredGpuBufferData(Buffers.DataBuffer buffer, string bufferName)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        if (buffer.TryGetGpuBufferData(out GpuBufferData data))
        {
            return data;
        }

        throw new NotSupportedException($"Buffer '{bufferName}' does not expose a direct GPU-compatible payload.");
    }

    private static GpuBufferData NormalizeIndexElementType(GpuBufferData data)
    {
        GpuBufferElementType elementType = data.ElementType switch
        {
            GpuBufferElementType.Int8 => GpuBufferElementType.UInt8,
            GpuBufferElementType.Int16 => GpuBufferElementType.UInt16,
            GpuBufferElementType.Int32 => GpuBufferElementType.UInt32,
            _ => data.ElementType,
        };

        return elementType == data.ElementType
            ? data
            : new GpuBufferData(
                data.Bytes,
                elementType,
                data.ElementCount,
                data.ValueCount,
                data.ComponentCount,
                data.ElementStrideBytes,
                data.ValueStrideBytes,
                data.ComponentSizeBytes);
    }

    private static bool TryBuildSkinningData(
        Mesh mesh,
        int vertexCount,
        out GpuBufferData skinIndexData,
        out GpuBufferData skinWeightData,
        out int influenceCount,
        out int boneCount)
    {
        skinIndexData = default;
        skinWeightData = default;
        influenceCount = 0;
        boneCount = 0;

        if (!mesh.HasSkinning || mesh.BoneIndices is null || mesh.BoneWeights is null)
        {
            return false;
        }

        if (mesh.SkinnedBones is not { Count: > 0 } skinnedBones)
        {
            return false;
        }

        Buffers.DataBuffer boneIndices = mesh.BoneIndices;
        Buffers.DataBuffer boneWeights = mesh.BoneWeights;
        int boneIndexElementWidth = boneIndices.ValueCount * boneIndices.ComponentCount;
        int boneWeightElementWidth = boneWeights.ValueCount * boneWeights.ComponentCount;
        influenceCount = boneIndexElementWidth;
        boneCount = skinnedBones.Count;
        if (influenceCount <= 0
            || boneCount <= 0
            || boneWeightElementWidth != influenceCount
            || boneIndices.ElementCount < vertexCount
            || boneWeights.ElementCount < vertexCount)
        {
            return false;
        }

        skinIndexData = GetRequiredGpuBufferData(boneIndices, nameof(mesh.BoneIndices));
        skinWeightData = GetRequiredGpuBufferData(boneWeights, nameof(mesh.BoneWeights));
        return true;
    }

}
