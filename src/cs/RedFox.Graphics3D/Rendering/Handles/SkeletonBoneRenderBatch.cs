using System;
using System.Numerics;
using System.Runtime.InteropServices;
using RedFox.Graphics3D.Rendering.Materials;

namespace RedFox.Graphics3D.Rendering.Handles;

internal sealed class SkeletonBoneRenderBatch : IDisposable
{
    internal const int FloatCountPerVertex = 13;
    private const int LineAttributeSlotCount = 6;

    private readonly IGraphicsDevice _graphicsDevice;
    private readonly IMaterialTypeRegistry _materialTypes;

    private IGpuBuffer? _lineBuffer;
    private int _lineBufferSizeBytes;
    private float[] _overlayVertices = [];
    private float[] _transparentVertices = [];
    private int _overlayFloatCount;
    private int _transparentFloatCount;
    private IGpuPipelineState? _pipeline;
    private ulong _frameIndex = ulong.MaxValue;
    private ulong _overlayRenderedFrameIndex = ulong.MaxValue;
    private ulong _transparentRenderedFrameIndex = ulong.MaxValue;
    private bool _disposed;

    public SkeletonBoneRenderBatch(IGraphicsDevice graphicsDevice, IMaterialTypeRegistry materialTypes)
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        _materialTypes = materialTypes ?? throw new ArgumentNullException(nameof(materialTypes));
    }

    public void Queue(
        ICommandList commandList,
        ReadOnlySpan<float> localVertices,
        in Matrix4x4 worldMatrix,
        RenderPhaseMask renderPhase,
        float lineHalfWidth)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(commandList);
        if (localVertices.IsEmpty)
        {
            return;
        }

        ResetForFrame(commandList.FrameIndex);
        if ((renderPhase & RenderPhaseMask.Overlay) != 0)
        {
            AppendVertices(ref _overlayVertices, ref _overlayFloatCount, localVertices, worldMatrix, lineHalfWidth);
            return;
        }

        if ((renderPhase & RenderPhaseMask.Transparent) != 0)
        {
            AppendVertices(ref _transparentVertices, ref _transparentFloatCount, localVertices, worldMatrix, lineHalfWidth);
        }
    }

    public void Render(
        ICommandList commandList,
        RenderPhase phase,
        in Matrix4x4 view,
        in Matrix4x4 projection,
        in Matrix4x4 sceneAxis,
        Vector3 cameraPosition,
        Vector2 viewportSize)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(commandList);

        int floatCount;
        ReadOnlySpan<float> vertices;
        if (phase == RenderPhase.Overlay)
        {
            if (_overlayRenderedFrameIndex == commandList.FrameIndex)
            {
                return;
            }

            _overlayRenderedFrameIndex = commandList.FrameIndex;
            floatCount = _overlayFloatCount;
            vertices = _overlayVertices.AsSpan(0, _overlayFloatCount);
        }
        else if (phase == RenderPhase.Transparent)
        {
            if (_transparentRenderedFrameIndex == commandList.FrameIndex)
            {
                return;
            }

            _transparentRenderedFrameIndex = commandList.FrameIndex;
            floatCount = _transparentFloatCount;
            vertices = _transparentVertices.AsSpan(0, _transparentFloatCount);
        }
        else
        {
            return;
        }

        if (floatCount == 0)
        {
            return;
        }

        EnsurePipeline();
        ReadOnlySpan<byte> vertexBytes = MemoryMarshal.AsBytes(vertices);
        if (_lineBuffer is null || _lineBufferSizeBytes < vertexBytes.Length)
        {
            _lineBuffer?.Dispose();
            _lineBuffer = _graphicsDevice.CreateBuffer(
                vertexBytes.Length,
                FloatCountPerVertex * sizeof(float),
                BufferUsage.Vertex | BufferUsage.DynamicWrite,
                vertexBytes);
            _lineBufferSizeBytes = vertexBytes.Length;
        }
        else
        {
            _graphicsDevice.UpdateBuffer(_lineBuffer, vertexBytes);
        }

        commandList.SetPipelineState(_pipeline ?? throw new InvalidOperationException("Skeleton pipeline was not created."));
        BindLineVertexBuffer(commandList, _lineBuffer);
        commandList.SetUniformMatrix4x4("Model", Matrix4x4.Identity);
        commandList.SetUniformMatrix4x4("SceneAxis", sceneAxis);
        commandList.SetUniformMatrix4x4("View", view);
        commandList.SetUniformMatrix4x4("Projection", projection);
        commandList.SetUniformVector2("ViewportSize", viewportSize);
        commandList.SetUniformFloat("LineHalfWidthPx", 1.0f);
        commandList.SetUniformVector3("CameraPosition", cameraPosition);
        commandList.SetUniformFloat("FadeStartDistance", 0.0f);
        commandList.SetUniformFloat("FadeEndDistance", 0.0f);
        commandList.Draw(floatCount / FloatCountPerVertex, 0);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _lineBuffer?.Dispose();
        _pipeline?.Dispose();
        _lineBuffer = null;
        _pipeline = null;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private static void AppendVertices(ref float[] vertices, ref int floatCount, ReadOnlySpan<float> source, in Matrix4x4 worldMatrix, float lineHalfWidth)
    {
        int requiredFloatCount = checked(floatCount + source.Length);
        if (vertices.Length < requiredFloatCount)
        {
            Array.Resize(ref vertices, Math.Max(requiredFloatCount, vertices.Length * 2));
        }

        Span<float> destination = vertices.AsSpan(floatCount, source.Length);
        int destinationOffset = 0;
        for (int sourceOffset = 0; sourceOffset < source.Length; sourceOffset += FloatCountPerVertex)
        {
            Vector3 lineStart = Vector3.Transform(new Vector3(source[sourceOffset], source[sourceOffset + 1], source[sourceOffset + 2]), worldMatrix);
            Vector3 lineEnd = Vector3.Transform(new Vector3(source[sourceOffset + 3], source[sourceOffset + 4], source[sourceOffset + 5]), worldMatrix);

            destination[destinationOffset++] = lineStart.X;
            destination[destinationOffset++] = lineStart.Y;
            destination[destinationOffset++] = lineStart.Z;
            destination[destinationOffset++] = lineEnd.X;
            destination[destinationOffset++] = lineEnd.Y;
            destination[destinationOffset++] = lineEnd.Z;
            destination[destinationOffset++] = source[sourceOffset + 6];
            destination[destinationOffset++] = source[sourceOffset + 7];
            destination[destinationOffset++] = source[sourceOffset + 8];
            destination[destinationOffset++] = source[sourceOffset + 9];
            destination[destinationOffset++] = source[sourceOffset + 10];
            destination[destinationOffset++] = source[sourceOffset + 11];
            destination[destinationOffset++] = source[sourceOffset + 12] * lineHalfWidth;
        }

        floatCount = requiredFloatCount;
    }

    private static void BindLineVertexBuffer(ICommandList commandList, IGpuBuffer lineBuffer)
    {
        for (int slot = 0; slot < LineAttributeSlotCount; slot++)
        {
            commandList.BindBuffer(slot, lineBuffer);
        }
    }

    private void EnsurePipeline()
    {
        if (_pipeline is not null)
        {
            return;
        }

        if (_materialTypes is not IMaterialPipelineProvider pipelineProvider)
        {
            throw new InvalidOperationException($"Material registry '{_materialTypes.GetType().Name}' does not provide runtime pipeline services.");
        }

        _pipeline = pipelineProvider.CreatePipeline(_graphicsDevice, "Skeleton");
    }

    private void ResetForFrame(ulong frameIndex)
    {
        if (_frameIndex == frameIndex)
        {
            return;
        }

        _frameIndex = frameIndex;
        _overlayFloatCount = 0;
        _transparentFloatCount = 0;
        _overlayRenderedFrameIndex = ulong.MaxValue;
        _transparentRenderedFrameIndex = ulong.MaxValue;
    }
}