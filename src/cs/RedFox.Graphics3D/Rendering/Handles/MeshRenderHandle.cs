using System;
using System.Numerics;
using RedFox.Graphics3D.Rendering.Backend;
using RedFox.Graphics3D.Rendering.Materials;

namespace RedFox.Graphics3D.Rendering.Handles;

/// <summary>
/// Owns mesh backend resources and coordinates material-driven rendering.
/// </summary>
internal sealed class MeshRenderHandle : RenderHandle
{
    private const int PositionBufferSlot = 0;
    private const int NormalBufferSlot = 1;
    private const int TangentBufferSlot = 2;
    private const int BitangentBufferSlot = 3;
    private const int ColorBufferSlot = 4;
    private const int UvBufferSlot = 5;
    private const int BoneIndexBufferSlot = 6;
    private const int BoneWeightBufferSlot = 7;
    private const int IndexBufferSlot = 8;

    private readonly IGraphicsDevice _graphicsDevice;
    private readonly IMaterialTypeRegistry _materialTypes;
    private readonly Mesh _mesh;

    private IGpuBuffer? _bitangentBuffer;
    private IGpuBuffer? _boneIndexBuffer;
    private IGpuBuffer? _boneWeightBuffer;
    private IGpuBuffer? _colorBuffer;
    private int _indexCount;
    private IGpuBuffer? _indexBuffer;
    private IGpuBuffer? _normalBuffer;
    private IGpuBuffer? _positionBuffer;
    private IGpuBuffer? _tangentBuffer;
    private IGpuBuffer? _uvBuffer;
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

        if (_mesh.Positions is not { ElementCount: > 0 } positions)
        {
            ReleaseBuffers();
            return;
        }

        _mesh.GenerateNormals(preserveExisting: true);

        _vertexCount = positions.ElementCount;
        _indexCount = _mesh.FaceIndices?.AsReadOnlySpan<uint>().Length ?? 0;

        EnsureBuffer(ref _positionBuffer, positions.AsReadOnlySpan<float>().Length * sizeof(float), positions.ComponentCount * sizeof(float), BufferUsage.Vertex);
        EnsureFloatBuffer(ref _normalBuffer, _mesh.Normals, BufferUsage.Vertex);
        EnsureFloatBuffer(ref _tangentBuffer, _mesh.Tangents, BufferUsage.Vertex);
        EnsureFloatBuffer(ref _bitangentBuffer, _mesh.BiTangents, BufferUsage.Vertex);
        EnsureFloatBuffer(ref _colorBuffer, _mesh.ColorLayers, BufferUsage.Vertex);
        EnsureFloatBuffer(ref _uvBuffer, _mesh.UVLayers, BufferUsage.Vertex);
        EnsureUIntBuffer(ref _boneIndexBuffer, _mesh.BoneIndices, BufferUsage.Vertex | BufferUsage.Structured);
        EnsureFloatBuffer(ref _boneWeightBuffer, _mesh.BoneWeights, BufferUsage.Vertex | BufferUsage.Structured);

        if (_mesh.FaceIndices is { ElementCount: > 0 } faceIndices)
        {
            EnsureBuffer(ref _indexBuffer, faceIndices.AsReadOnlySpan<uint>().Length * sizeof(uint), sizeof(uint), BufferUsage.Index);
        }
        else
        {
            DisposeBuffer(ref _indexBuffer);
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

        if (phase != RenderPhase.Opaque || _vertexCount <= 0 || _positionBuffer is null)
        {
            return;
        }

        if (_mesh.Materials is { Count: > 0 } materials)
        {
            MaterialRenderHandle materialHandle = EnsureMaterialHandle(materials[0]);
            materialHandle.BindResources(commandList);
        }

        commandList.SetUniformMatrix4x4("uModel", _mesh.GetActiveWorldMatrix());
        commandList.SetUniformMatrix4x4("uView", view);
        commandList.SetUniformMatrix4x4("uProjection", projection);
        commandList.SetUniformMatrix4x4("uSceneAxis", sceneAxis);
        commandList.SetUniformVector3("uCameraPosition", cameraPosition);
        commandList.SetUniformVector2("uViewportSize", viewportSize);

        commandList.BindBuffer(PositionBufferSlot, _positionBuffer);
        BindOptionalBuffer(commandList, NormalBufferSlot, _normalBuffer);
        BindOptionalBuffer(commandList, TangentBufferSlot, _tangentBuffer);
        BindOptionalBuffer(commandList, BitangentBufferSlot, _bitangentBuffer);
        BindOptionalBuffer(commandList, ColorBufferSlot, _colorBuffer);
        BindOptionalBuffer(commandList, UvBufferSlot, _uvBuffer);
        BindOptionalBuffer(commandList, BoneIndexBufferSlot, _boneIndexBuffer);
        BindOptionalBuffer(commandList, BoneWeightBufferSlot, _boneWeightBuffer);

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
        ReleaseBuffers();
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

    private static void BindOptionalBuffer(ICommandList commandList, int slot, IGpuBuffer? buffer)
    {
        if (buffer is not null)
        {
            commandList.BindBuffer(slot, buffer);
        }
    }

    private void EnsureFloatBuffer(ref IGpuBuffer? buffer, Buffers.DataBuffer? source, BufferUsage usage)
    {
        if (source is null || source.ElementCount == 0)
        {
            DisposeBuffer(ref buffer);
            return;
        }

        EnsureBuffer(ref buffer, source.AsReadOnlySpan<float>().Length * sizeof(float), source.ComponentCount * sizeof(float), usage);
    }

    private void EnsureUIntBuffer(ref IGpuBuffer? buffer, Buffers.DataBuffer? source, BufferUsage usage)
    {
        if (source is null || source.ElementCount == 0)
        {
            DisposeBuffer(ref buffer);
            return;
        }

        EnsureBuffer(ref buffer, source.AsReadOnlySpan<uint>().Length * sizeof(uint), source.ComponentCount * sizeof(uint), usage);
    }

    private void EnsureBuffer(ref IGpuBuffer? buffer, int sizeBytes, int stride, BufferUsage usage)
    {
        if (sizeBytes <= 0 || stride <= 0)
        {
            DisposeBuffer(ref buffer);
            return;
        }

        if (buffer is not null)
        {
            return;
        }

        buffer = _graphicsDevice.CreateBuffer(sizeBytes, stride, usage);
    }

    private void DisposeBuffer(ref IGpuBuffer? buffer)
    {
        buffer?.Dispose();
        buffer = null;
    }

    private void ReleaseBuffers()
    {
        DisposeBuffer(ref _positionBuffer);
        DisposeBuffer(ref _normalBuffer);
        DisposeBuffer(ref _tangentBuffer);
        DisposeBuffer(ref _bitangentBuffer);
        DisposeBuffer(ref _colorBuffer);
        DisposeBuffer(ref _uvBuffer);
        DisposeBuffer(ref _boneIndexBuffer);
        DisposeBuffer(ref _boneWeightBuffer);
        DisposeBuffer(ref _indexBuffer);
        _vertexCount = 0;
        _indexCount = 0;
    }
}