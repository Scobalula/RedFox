using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using RedFox.Graphics3D.Rendering.Materials;

namespace RedFox.Graphics3D.Rendering.Handles;

internal sealed class SkeletonBoneBatch(IGraphicsDevice graphicsDevice, IMaterialTypeRegistry materialTypes) : RenderHandle
{
    private const int TransparentLayer	= 0;
    private const int OverlayLayer		= 1;
    private const int VertexStrideBytes	= SkeletonBoneRenderHandle.FloatCountPerVertex * sizeof(float);

    private readonly IGraphicsDevice		_graphicsDevice		= graphicsDevice;
    private readonly IMaterialTypeRegistry	_materialTypes		= materialTypes;
    private readonly List<float>[]			_vertices			= [[], []];
    private readonly float[][]				_uploadedVertices	= [[], []];
    private readonly IGpuBuffer?[]			_buffers			= new IGpuBuffer?[2];
    private readonly bool[]					_drawn				= new bool[2];
    private ulong							_frameIndex			= ulong.MaxValue;
    private IGpuPipelineState?				_pipeline;

    public override void Update(ICommandList commandList)
    {
        ThrowIfDisposed();

        if (_frameIndex == commandList.FrameIndex)
            return;

        _frameIndex = commandList.FrameIndex;

        for (int layer = 0; layer < _vertices.Length; layer++)
        {
            _vertices[layer].Clear();
            _drawn[layer] = false;
        }
    }

    public override void Render(ICommandList commandList, RenderFlags phase, in Matrix4x4 view, in Matrix4x4 projection, in Matrix4x4 sceneAxis, Vector3 cameraPosition, Vector2 viewportSize)
    {
        ThrowIfDisposed();

        int layer = GetLayer(phase);
        if (_drawn[layer])
            return;

        _drawn[layer] = true;

        ReadOnlySpan<float> vertices = CollectionsMarshal.AsSpan(_vertices[layer]);
        if (vertices.IsEmpty)
            return;

        IGpuBuffer buffer = Upload(layer, vertices);
        IGpuPipelineState pipeline = AcquirePipeline();

        commandList.SetPipelineState(pipeline);
        BindVertexAttribute(commandList, pipeline, buffer, "LineStart");
        BindVertexAttribute(commandList, pipeline, buffer, "LineEnd");
        BindVertexAttribute(commandList, pipeline, buffer, "Color");
        BindVertexAttribute(commandList, pipeline, buffer, "Along");
        BindVertexAttribute(commandList, pipeline, buffer, "Side");
        BindVertexAttribute(commandList, pipeline, buffer, "WidthScale");
        commandList.SetUniformMatrix4x4("Model", Matrix4x4.Identity);
        commandList.SetUniformMatrix4x4("View", view);
        commandList.SetUniformMatrix4x4("Projection", projection);
        commandList.SetUniformMatrix4x4("SceneAxis", sceneAxis);
        commandList.SetUniformVector3("CameraPosition", cameraPosition);
        commandList.SetUniformVector2("ViewportSize", viewportSize);
        commandList.SetUniformFloat("LineHalfWidthPx", 1.0f);
        commandList.Draw(vertices.Length / SkeletonBoneRenderHandle.FloatCountPerVertex, 0);
    }

    public void Append(RenderFlags phase, ReadOnlySpan<float> vertices)
    {
        _vertices[GetLayer(phase)].AddRange(vertices);
    }

    protected override void ReleaseResources()
    {
        for (int layer = 0; layer < _buffers.Length; layer++)
        {
            _buffers[layer]?.Dispose();
            _buffers[layer] = null;
            _uploadedVertices[layer] = [];
        }

        if (_pipeline is not null && _materialTypes is IMaterialPipelineProvider pipelineProvider)
            pipelineProvider.ReleasePipeline(SkeletonBoneRenderHandle.SkeletonMaterialTypeName, _pipeline);

        _pipeline = null;
    }

    private static int GetLayer(RenderFlags phase) => phase == RenderFlags.Overlay ? OverlayLayer : TransparentLayer;

    private static void BindVertexAttribute(ICommandList commandList, IGpuPipelineState pipeline, IGpuBuffer buffer, string attributeName)
    {
        if (pipeline.TryGetBufferSlot(attributeName, out int slot))
            commandList.BindBuffer(slot, buffer);
    }

    private IGpuBuffer Upload(int layer, ReadOnlySpan<float> vertices)
    {
        IGpuBuffer? buffer = _buffers[layer];
        if (buffer is not null && !buffer.IsDisposed && vertices.SequenceEqual(_uploadedVertices[layer]))
            return buffer;

        ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(vertices);
        if (buffer is null || buffer.IsDisposed || buffer.SizeBytes < bytes.Length)
        {
            buffer?.Dispose();
            buffer = _graphicsDevice.CreateBuffer(bytes.Length, VertexStrideBytes, BufferUsage.Vertex | BufferUsage.DynamicWrite, GpuBufferElementType.Float32, bytes);
            _buffers[layer] = buffer;
        }
        else
        {
            _graphicsDevice.UpdateBuffer(buffer, bytes);
        }

        if (_uploadedVertices[layer].Length != vertices.Length)
            _uploadedVertices[layer] = new float[vertices.Length];

        vertices.CopyTo(_uploadedVertices[layer]);
        return buffer;
    }

    private IGpuPipelineState AcquirePipeline()
    {
        if (_pipeline is not null && !_pipeline.IsDisposed)
            return _pipeline;

        if (_materialTypes is not IMaterialPipelineProvider pipelineProvider)
            throw new InvalidOperationException($"Material registry '{_materialTypes.GetType().Name}' does not provide runtime pipeline services.");

        _pipeline = pipelineProvider.AcquirePipeline(_graphicsDevice, SkeletonBoneRenderHandle.SkeletonMaterialTypeName);
        return _pipeline;
    }
}
