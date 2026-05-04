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
    private readonly Mesh _mesh = mesh;

    private int _indexComponentCount;
    private Matrix4x4[] _lastSkinTransforms = Array.Empty<Matrix4x4>();

    private RenderPhaseMask _renderPhases;
    private Matrix4x4[] _skinTransformsScratch = Array.Empty<Matrix4x4>();
    private int _vertexCount;

    /// <inheritdoc/>
    public override RenderPhaseMask RenderPhases => _renderPhases;

    /// <inheritdoc/>
    public override bool RequiresPerFrameUpdate => true;

    /// <inheritdoc/>
    public override void Update(ICommandList commandList)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(commandList);

        _renderPhases = RenderPhaseMask.None;

        if (_buffers.Count == 0)
        {
            _vertexCount = _mesh.Positions?.ElementCount ?? 0;
            _indexComponentCount = _mesh.FaceIndices?.TotalComponentCount ?? 0;
            if (_vertexCount <= 0)
            {
                ReleaseBuffers();
                return;
            }

            if (_mesh.Normals is not { ElementCount: > 0 })
            {
                _mesh.GenerateNormals();
            }

            if (_mesh.Normals is not { ElementCount: > 0 })
            {
                ReleaseBuffers();
                return;
            }

            _buffers.Add(MeshGpuBufferBinding.CreateVertex(_mesh.Positions, nameof(Mesh.Positions)));
            _buffers.Add(MeshGpuBufferBinding.CreateVertex(_mesh.Normals, nameof(Mesh.Normals)));
            _buffers.Add(MeshGpuBufferBinding.CreateIndex(_mesh.FaceIndices, nameof(Mesh.FaceIndices)));
            _buffers.Add(MeshGpuBufferBinding.CreateShaderResource(_mesh.BoneIndices, "BoneIndexBuffer", BufferUsage.Sampled, normalizeIndexElementType: true));
            _buffers.Add(MeshGpuBufferBinding.CreateShaderResource(_mesh.BoneWeights, "BoneWeightBuffer", BufferUsage.Sampled, normalizeIndexElementType: false));
            _buffers.Add(MeshGpuBufferBinding.CreateShaderResource("SkinTransformBuffer", BufferUsage.Sampled | BufferUsage.DynamicWrite, normalizeIndexElementType: false, UpdateSkinTransformBuffer));
        }

        if (_vertexCount <= 0)
        {
            ReleaseBuffers();
            return;
        }

        foreach (MeshGpuBufferBinding buffer in _buffers)
        {
            buffer.Update(_graphicsDevice);
        }

        _renderPhases = RenderPhaseMask.Opaque;
    }

    /// <inheritdoc/>
    public override void Render(ICommandList commandList, RenderPhase phase, in Matrix4x4 view, in Matrix4x4 projection, in Matrix4x4 sceneAxis, Vector3 cameraPosition, Vector2 viewportSize)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(commandList);

        if (phase != RenderPhase.Opaque || _renderPhases != RenderPhaseMask.Opaque || _vertexCount <= 0)
        {
            return;
        }

        if (_mesh.Materials is not { Count: > 0 })
        {
            return;
        }

        for (int i = 0; i < _mesh.Materials.Count; i++)
        {
            Material material = _mesh.Materials[i];
            if (material.GraphicsHandle is not MaterialRenderHandle materialHandle)
            {
                continue;
            }

            materialHandle.BindResources(commandList);
            if (materialHandle.Pipeline is not { } pipeline)
            {
                continue;
            }

            commandList.SetUniformMatrix4x4("Model", _mesh.GetBindWorldMatrix());
            commandList.SetUniformMatrix4x4("View", view);
            commandList.SetUniformMatrix4x4("Projection", projection);
            commandList.SetUniformMatrix4x4("SceneAxis", sceneAxis);
            commandList.SetUniformVector3("CameraPosition", cameraPosition);
            commandList.SetUniformInt("SkinInfluenceCount", _mesh.BoneIndices is null ? 0 : _mesh.BoneIndices.ValueCount);

            foreach (MeshGpuBufferBinding buffer in _buffers)
            {
                buffer.Bind(commandList, pipeline);
            }

            if (_indexComponentCount > 0)
            {
                commandList.DrawIndexed(_indexComponentCount, 0, 0);
            }
            else
            {
                commandList.Draw(_vertexCount, 0);
            }

            return;
        }
    }

    /// <inheritdoc/>
    protected override void ReleaseCore()
    {
        ReleaseBuffers();
    }

    private bool UpdateSkinTransformBuffer(MeshGpuBufferBinding binding, IGraphicsDevice graphicsDevice)
    {
        if (_mesh.BoneIndices is null || _mesh.BoneWeights is null || _mesh.SkinnedBones is null)
        {
            binding.Release();
            return false;
        }

        Span<Matrix4x4> matrixBuffer = _mesh.SkinnedBones.Count > 128 ? new Matrix4x4[_mesh.SkinnedBones.Count] : stackalloc Matrix4x4[_mesh.SkinnedBones.Count];
        _mesh.CopySkinTransforms(matrixBuffer);

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
    }

    private void ReleaseBuffers()
    {
        foreach (MeshGpuBufferBinding buffer in _buffers)
        {
            buffer.Release();
        }

        _vertexCount = 0;
        _indexComponentCount = 0;
        _renderPhases = RenderPhaseMask.None;
    }
}
