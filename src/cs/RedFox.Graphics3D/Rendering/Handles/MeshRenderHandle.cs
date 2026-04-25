using System;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using RedFox.Graphics3D.Rendering.Backend;
using RedFox.Graphics3D.Rendering.Materials;

namespace RedFox.Graphics3D.Rendering.Handles;

/// <summary>
/// Owns mesh backend resources and coordinates material-driven rendering.
/// </summary>
internal sealed class MeshRenderHandle : RenderHandle
{
    private const int ComputeSourcePositionBufferSlot = 0;
    private const int ComputeSourceNormalBufferSlot = 1;
    private const int ComputeBoneIndexBufferSlot = 2;
    private const int ComputeBoneWeightBufferSlot = 3;
    private const int ComputeSkinTransformBufferSlot = 4;
    private const int ComputeOutputPositionBufferSlot = 5;
    private const int ComputeOutputNormalBufferSlot = 6;
    private const int DrawPositionBufferSlot = 0;
    private const int DrawNormalBufferSlot = 1;
    private const int IndexBufferSlot = 8;

    private readonly IGraphicsDevice _graphicsDevice;
    private readonly IMaterialTypeRegistry _materialTypes;
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
    private IGpuPipelineState? _skinningPipeline;
    private int _skinInfluenceCount;
    private IGpuBuffer? _skinTransformBuffer;
    private int _skinTransformBufferSizeBytes;
    private Matrix4x4[] _skinTransformsScratch = Array.Empty<Matrix4x4>();
    private IGpuBuffer? _sourceNormalBuffer;
    private int _sourceNormalBufferSizeBytes;
    private IGpuBuffer? _sourcePositionBuffer;
    private int _sourcePositionBufferSizeBytes;
    private bool _skinningDispatchedThisFrame;
    private int _vertexCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="MeshRenderHandle"/> class.
    /// </summary>
    /// <param name="graphicsDevice">The graphics device that creates mesh resources.</param>
    /// <param name="materialTypes">The material-type registry used to resolve pipelines.</param>
    /// <param name="mesh">The mesh node represented by this handle.</param>
    public MeshRenderHandle(IGraphicsDevice graphicsDevice, IMaterialTypeRegistry materialTypes, Mesh mesh)
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        _materialTypes = materialTypes ?? throw new ArgumentNullException(nameof(materialTypes));
        _mesh = mesh ?? throw new ArgumentNullException(nameof(mesh));
    }

    /// <inheritdoc/>
    public override void Update(ICommandList commandList)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(commandList);

        _skinningDispatchedThisFrame = false;

        if (_mesh.Positions is not { ElementCount: > 0 } positions)
        {
            ReleaseBuffers();
            return;
        }

        if (_mesh.Normals is not { ElementCount: > 0 })
        {
            _mesh.GenerateNormals(preserveExisting: true);
        }

        if (_mesh.Normals is not { ElementCount: > 0 } normals)
        {
            ReleaseBuffers();
            return;
        }

        _vertexCount = positions.ElementCount;

        bool geometryChanged = GeometryChanged(positions, normals, _mesh.FaceIndices);
        bool computeSkinningRequested = _graphicsDevice.SupportsCompute
            && _mesh.HasSkinning
            && _mesh.BoneIndices is { ElementCount: > 0 }
            && _mesh.BoneWeights is { ElementCount: > 0 }
            && _mesh.SkinnedBones is { Count: > 0 };

        if (computeSkinningRequested)
        {
            bool skinningInputsChanged = geometryChanged
                || !_cachedUsesComputeSkinning
                || !ReferenceEquals(_cachedBoneIndices, _mesh.BoneIndices)
                || !ReferenceEquals(_cachedBoneWeights, _mesh.BoneWeights)
                || _sourcePositionBuffer is null
                || _sourceNormalBuffer is null
                || _positionBuffer is null
                || _normalBuffer is null
                || _boneIndexBuffer is null
                || _boneWeightBuffer is null;

            if (skinningInputsChanged)
            {
                ReadOnlySpan<byte> positionBytes = MemoryMarshal.AsBytes(positions.AsReadOnlySpan<float>());
                ReadOnlySpan<byte> normalBytes = MemoryMarshal.AsBytes(normals.AsReadOnlySpan<float>());
                if (!TryBuildSkinningData(_mesh, _vertexCount, out uint[] skinIndexData, out float[] skinWeightData, out int influenceCount, out int boneCount))
                {
                    ReleaseComputeBuffers();
                    computeSkinningRequested = false;
                }
                else
                {
                    _skinInfluenceCount = influenceCount;
                    _boneCount = boneCount;

                    EnsurePipeline(ref _skinningPipeline, "Skinning");
                    EnsureOrUpdateBuffer(ref _sourcePositionBuffer, ref _sourcePositionBufferSizeBytes, positions.ComponentCount * sizeof(float), BufferUsage.ShaderStorage, positionBytes);
                    EnsureOrUpdateBuffer(ref _sourceNormalBuffer, ref _sourceNormalBufferSizeBytes, normals.ComponentCount * sizeof(float), BufferUsage.ShaderStorage, normalBytes);
                    EnsureOrUpdateBuffer(ref _positionBuffer, ref _positionBufferSizeBytes, positions.ComponentCount * sizeof(float), BufferUsage.Vertex | BufferUsage.ShaderStorage | BufferUsage.DynamicWrite, positionBytes);
                    EnsureOrUpdateBuffer(ref _normalBuffer, ref _normalBufferSizeBytes, normals.ComponentCount * sizeof(float), BufferUsage.Vertex | BufferUsage.ShaderStorage | BufferUsage.DynamicWrite, normalBytes);
                    EnsureOrUpdateBuffer(ref _boneIndexBuffer, ref _boneIndexBufferSizeBytes, sizeof(uint), BufferUsage.Structured | BufferUsage.ShaderStorage, MemoryMarshal.AsBytes(skinIndexData.AsSpan()));
                    EnsureOrUpdateBuffer(ref _boneWeightBuffer, ref _boneWeightBufferSizeBytes, sizeof(float), BufferUsage.Structured | BufferUsage.ShaderStorage, MemoryMarshal.AsBytes(skinWeightData.AsSpan()));
                    _cachedBoneIndices = _mesh.BoneIndices;
                    _cachedBoneWeights = _mesh.BoneWeights;
                    _cachedUsesComputeSkinning = true;
                }
            }

            if (computeSkinningRequested)
            {
                _cachedPositions = positions;
                _cachedNormals = normals;
                _cachedFaceIndices = _mesh.FaceIndices;
                _cachedUsesComputeSkinning = true;
            }

            UpdateSkinTransformBuffer();
        }

        if (!computeSkinningRequested)
        {
            _skinInfluenceCount = 0;
            _boneCount = 0;

            if (geometryChanged || _cachedUsesComputeSkinning || _positionBuffer is null || _normalBuffer is null)
            {
                ReadOnlySpan<byte> positionBytes = MemoryMarshal.AsBytes(positions.AsReadOnlySpan<float>());
                ReadOnlySpan<byte> normalBytes = MemoryMarshal.AsBytes(normals.AsReadOnlySpan<float>());
                EnsureOrUpdateBuffer(ref _positionBuffer, ref _positionBufferSizeBytes, positions.ComponentCount * sizeof(float), BufferUsage.Vertex, positionBytes);
                EnsureOrUpdateBuffer(ref _normalBuffer, ref _normalBufferSizeBytes, normals.ComponentCount * sizeof(float), BufferUsage.Vertex, normalBytes);
                ReleaseComputeBuffers();
                _cachedPositions = positions;
                _cachedNormals = normals;
                _cachedBoneIndices = null;
                _cachedBoneWeights = null;
                _cachedUsesComputeSkinning = false;
            }
        }

        if (geometryChanged || _indexBuffer is null != (_mesh.FaceIndices is not { ElementCount: > 0 }))
        {
            if (_mesh.FaceIndices is { ElementCount: > 0 } faceIndices)
            {
                ReadOnlySpan<byte> indexBytes = MemoryMarshal.AsBytes(faceIndices.AsReadOnlySpan<uint>());
                EnsureOrUpdateBuffer(ref _indexBuffer, ref _indexBufferSizeBytes, sizeof(uint), BufferUsage.Index, indexBytes);
                _indexCount = faceIndices.AsReadOnlySpan<uint>().Length;
            }
            else
            {
                DisposeBuffer(ref _indexBuffer, ref _indexBufferSizeBytes);
                _indexCount = 0;
            }

            _cachedFaceIndices = _mesh.FaceIndices;
        }

        if (_mesh.Materials is { Count: > 0 } materials)
        {
            MaterialRenderHandle materialHandle = EnsureMaterialHandle(materials[0]);
            materialHandle.Update(commandList);
        }
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
        ArgumentNullException.ThrowIfNull(commandList);

        if (phase == RenderPhase.SkinningCompute)
        {
            DispatchSkinning(commandList);
            return;
        }

        if (phase != RenderPhase.Opaque || _vertexCount <= 0 || _positionBuffer is null || _normalBuffer is null)
        {
            return;
        }

        if (_mesh.Materials is { Count: > 0 } materials)
        {
            MaterialRenderHandle materialHandle = EnsureMaterialHandle(materials[0]);
            materialHandle.BindResources(commandList);
        }
        else
        {
            EnsurePipeline(ref _defaultPipeline, "Default");
            if (_defaultPipeline is not null)
            {
                commandList.SetPipelineState(_defaultPipeline);
                commandList.SetUniformVector4("BaseColor", Vector4.One);
                commandList.SetUniformFloat("MaterialSpecularStrength", 0.28f);
                commandList.SetUniformFloat("MaterialSpecularPower", 32.0f);
            }
        }

        Matrix4x4 modelMatrix = _skinningDispatchedThisFrame ? Matrix4x4.Identity : _mesh.GetBindWorldMatrix();

        commandList.SetUniformMatrix4x4("Model", modelMatrix);
        commandList.SetUniformMatrix4x4("View", view);
        commandList.SetUniformMatrix4x4("Projection", projection);
        commandList.SetUniformMatrix4x4("SceneAxis", sceneAxis);
        commandList.SetUniformVector3("CameraPosition", cameraPosition);

        commandList.BindBuffer(DrawPositionBufferSlot, _positionBuffer);
        commandList.BindBuffer(DrawNormalBufferSlot, _normalBuffer);

        if (_indexBuffer is not null && _indexCount > 0)
        {
            commandList.BindBuffer(IndexBufferSlot, _indexBuffer);
            commandList.DrawIndexed(_indexCount, 0, 0);
            return;
        }

        commandList.Draw(_vertexCount, 0);
    }

    /// <inheritdoc/>
    protected override void ReleaseCore()
    {
        _defaultPipeline?.Dispose();
        _defaultPipeline = null;
        _skinningPipeline?.Dispose();
        _skinningPipeline = null;
        ReleaseBuffers();
    }

    private void DispatchSkinning(ICommandList commandList)
    {
        if (_skinningPipeline is null
            || _sourcePositionBuffer is null
            || _sourceNormalBuffer is null
            || _boneIndexBuffer is null
            || _boneWeightBuffer is null
            || _skinTransformBuffer is null
            || _positionBuffer is null
            || _normalBuffer is null
            || _vertexCount <= 0)
        {
            return;
        }

        commandList.SetPipelineState(_skinningPipeline);
        commandList.BindBuffer(ComputeSourcePositionBufferSlot, _sourcePositionBuffer);
        commandList.BindBuffer(ComputeSourceNormalBufferSlot, _sourceNormalBuffer);
        commandList.BindBuffer(ComputeBoneIndexBufferSlot, _boneIndexBuffer);
        commandList.BindBuffer(ComputeBoneWeightBufferSlot, _boneWeightBuffer);
        commandList.BindBuffer(ComputeSkinTransformBufferSlot, _skinTransformBuffer);
        commandList.BindBuffer(ComputeOutputPositionBufferSlot, _positionBuffer);
        commandList.BindBuffer(ComputeOutputNormalBufferSlot, _normalBuffer);
        commandList.SetUniformInt("VertexCount", _vertexCount);
        commandList.SetUniformInt("SkinInfluenceCount", _skinInfluenceCount);
        commandList.Dispatch((_vertexCount + 63) / 64, 1, 1);
        commandList.MemoryBarrier();
        _skinningDispatchedThisFrame = true;
    }

    private MaterialRenderHandle EnsureMaterialHandle(Material material)
    {
        if (material.GraphicsHandle is MaterialRenderHandle existingHandle && existingHandle.IsOwnedBy(_graphicsDevice))
        {
            return existingHandle;
        }

        if (material.GraphicsHandle is not null)
        {
            material.GraphicsHandle.Release();
            material.GraphicsHandle.Dispose();
        }

        IRenderHandle? renderHandle = material.CreateRenderHandle(_graphicsDevice, _materialTypes);
        MaterialRenderHandle materialHandle = renderHandle as MaterialRenderHandle
            ?? throw new InvalidOperationException($"Material '{material.Name}' did not create a {nameof(MaterialRenderHandle)}.");

        material.GraphicsHandle = materialHandle;
        return materialHandle;
    }

    private void EnsureOrUpdateBuffer(ref IGpuBuffer? buffer, ref int sizeBytes, int stride, BufferUsage usage, ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty || stride <= 0)
        {
            DisposeBuffer(ref buffer, ref sizeBytes);
            return;
        }

        if (buffer is null || sizeBytes != data.Length)
        {
            buffer?.Dispose();
            buffer = _graphicsDevice.CreateBuffer(data.Length, stride, usage, data);
            sizeBytes = data.Length;
            return;
        }

        _graphicsDevice.UpdateBuffer(buffer, data);
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

        MaterialTypeDefinition definition = _materialTypes.Get(typeName);
        pipeline = definition.BuildPipeline(_graphicsDevice);
    }

    private void UpdateSkinTransformBuffer()
    {
        if (_boneCount <= 0)
        {
            DisposeBuffer(ref _skinTransformBuffer, ref _skinTransformBufferSizeBytes);
            return;
        }

        EnsureSkinTransformsScratchCapacity(_boneCount);
        int copiedTransforms = _mesh.CopySkinTransforms(_skinTransformsScratch);
        if (copiedTransforms < _boneCount)
        {
            DisposeBuffer(ref _skinTransformBuffer, ref _skinTransformBufferSizeBytes);
            return;
        }

        ReadOnlySpan<byte> transformBytes = MemoryMarshal.AsBytes(_skinTransformsScratch.AsSpan(0, copiedTransforms));
        EnsureOrUpdateBuffer(ref _skinTransformBuffer, ref _skinTransformBufferSizeBytes, 16 * sizeof(float), BufferUsage.ShaderStorage | BufferUsage.DynamicWrite, transformBytes);
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
        DisposeBuffer(ref _sourcePositionBuffer, ref _sourcePositionBufferSizeBytes);
        DisposeBuffer(ref _sourceNormalBuffer, ref _sourceNormalBufferSizeBytes);
        DisposeBuffer(ref _boneIndexBuffer, ref _boneIndexBufferSizeBytes);
        DisposeBuffer(ref _boneWeightBuffer, ref _boneWeightBufferSizeBytes);
        DisposeBuffer(ref _skinTransformBuffer, ref _skinTransformBufferSizeBytes);
        _skinningPipeline?.Dispose();
        _skinningPipeline = null;
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
    }

    private static bool TryBuildSkinningData(
        Mesh mesh,
        int vertexCount,
        out uint[] skinIndexData,
        out float[] skinWeightData,
        out int influenceCount,
        out int boneCount)
    {
        skinIndexData = Array.Empty<uint>();
        skinWeightData = Array.Empty<float>();
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
        influenceCount = Math.Min(boneIndices.ValueCount, boneWeights.ValueCount);
        boneCount = skinnedBones.Count;
        if (influenceCount <= 0 || boneCount <= 0)
        {
            return false;
        }

        ReadOnlySpan<uint> boneIndexComponents = boneIndices.AsReadOnlySpan<uint>();
        ReadOnlySpan<float> boneWeightComponents = boneWeights.AsReadOnlySpan<float>();
        int boneIndexElementWidth = boneIndices.ValueCount * boneIndices.ComponentCount;
        int boneWeightElementWidth = boneWeights.ValueCount * boneWeights.ComponentCount;

        skinIndexData = new uint[vertexCount * influenceCount];
        skinWeightData = new float[vertexCount * influenceCount];

        for (int vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
        {
            int boneIndexBase = vertexIndex * boneIndexElementWidth;
            int boneWeightBase = vertexIndex * boneWeightElementWidth;
            int outputBase = vertexIndex * influenceCount;

            boneIndexComponents.Slice(boneIndexBase, influenceCount).CopyTo(skinIndexData.AsSpan(outputBase, influenceCount));
            boneWeightComponents.Slice(boneWeightBase, influenceCount).CopyTo(skinWeightData.AsSpan(outputBase, influenceCount));
        }

        for (int packedIndex = 0; packedIndex < skinIndexData.Length; packedIndex++)
        {
            if (skinIndexData[packedIndex] >= boneCount)
            {
                int vertexIndex = packedIndex / influenceCount;
                throw new InvalidDataException($"Mesh '{mesh.Name}' contains an invalid skin index {skinIndexData[packedIndex]} at vertex {vertexIndex}.");
            }
        }

        return true;
    }
}