using RedFox.Graphics3D.Rendering.Backend;
using RedFox.Graphics3D.OpenGL.Resources;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace RedFox.Graphics3D.OpenGL;

/// <summary>
/// Represents the concrete OpenGL immediate-mode command list implementation.
/// </summary>
internal sealed class OpenGlCommandList : ICommandList, IDisposable
{
    private readonly Dictionary<int, OpenGlBuffer> _boundBuffers = [];
    private readonly OpenGlContext _context;
    private OpenGlPipelineState? _currentPipelineState;
    private bool _disposed;
    private uint _vertexArrayHandle;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenGlCommandList"/> class.
    /// </summary>
    /// <param name="context">The owning OpenGL context wrapper.</param>
    public OpenGlCommandList(OpenGlContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _vertexArrayHandle = context.CreateVertexArray();
    }

    /// <inheritdoc/>
    public void Reset()
    {
        ThrowIfDisposed();
        _boundBuffers.Clear();
        _currentPipelineState = null;
    }

    /// <inheritdoc/>
    public void SetViewport(int width, int height)
    {
        ThrowIfDisposed();
        _context.SetViewport(width, height);
    }

    /// <inheritdoc/>
    public void SetRenderTarget(IGpuRenderTarget? renderTarget)
    {
        ThrowIfDisposed();

        GL gl = _context.Gl;
        uint handle = renderTarget is OpenGlRenderTarget openGlRenderTarget
            ? openGlRenderTarget.Handle
            : 0u;
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, handle);
    }

    /// <inheritdoc/>
    public void ClearRenderTarget(float red, float green, float blue, float alpha, float depth)
    {
        ThrowIfDisposed();

        GL gl = _context.Gl;
        gl.ClearColor(red, green, blue, alpha);
        gl.ClearDepth(depth);
        gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));
    }

    /// <inheritdoc/>
    public void SetPipelineState(IGpuPipelineState pipelineState)
    {
        ThrowIfDisposed();

        _currentPipelineState = pipelineState as OpenGlPipelineState
            ?? throw new InvalidOperationException($"Expected {nameof(OpenGlPipelineState)}.");

        ApplyPipelineState(_currentPipelineState);
    }

    /// <inheritdoc/>
    public void BindBuffer(int slot, IGpuBuffer buffer)
    {
        ThrowIfDisposed();

        OpenGlBuffer openGlBuffer = buffer as OpenGlBuffer
            ?? throw new InvalidOperationException($"Expected {nameof(OpenGlBuffer)}.");
        _boundBuffers[slot] = openGlBuffer;

        if (openGlBuffer.Usage.HasFlag(BufferUsage.Index))
        {
            _context.Gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, openGlBuffer.Handle);
            return;
        }

        if (_currentPipelineState is null)
        {
            return;
        }

        if (openGlBuffer.Usage.HasFlag(BufferUsage.ShaderStorage) || openGlBuffer.Usage.HasFlag(BufferUsage.Structured))
        {
            _context.Gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, (uint)slot, openGlBuffer.Handle);
            return;
        }

        ConfigureVertexAttribute(slot, openGlBuffer, _currentPipelineState);
    }

    /// <inheritdoc/>
    public void BindTexture(int slot, IGpuTexture texture)
    {
        ThrowIfDisposed();

        OpenGlTexture openGlTexture = texture as OpenGlTexture
            ?? throw new InvalidOperationException($"Expected {nameof(OpenGlTexture)}.");
        GL gl = _context.Gl;
        gl.ActiveTexture(TextureUnit.Texture0 + slot);
        gl.BindTexture(TextureTarget.Texture2D, openGlTexture.Handle);
    }

    /// <inheritdoc/>
    public void SetUniformInt(ReadOnlySpan<char> name, int value)
    {
        GetActiveProgram().SetInt(name.ToString(), value);
    }

    /// <inheritdoc/>
    public void SetUniformFloat(ReadOnlySpan<char> name, float value)
    {
        GetActiveProgram().SetFloat(name.ToString(), value);
    }

    /// <inheritdoc/>
    public void SetUniformVector2(ReadOnlySpan<char> name, Vector2 value)
    {
        GetActiveProgram().SetVector2(name.ToString(), value);
    }

    /// <inheritdoc/>
    public void SetUniformVector3(ReadOnlySpan<char> name, Vector3 value)
    {
        GetActiveProgram().SetVector3(name.ToString(), value);
    }

    /// <inheritdoc/>
    public void SetUniformVector4(ReadOnlySpan<char> name, Vector4 value)
    {
        GetActiveProgram().SetVector4(name.ToString(), value);
    }

    /// <inheritdoc/>
    public void SetUniformMatrix4x4(ReadOnlySpan<char> name, Matrix4x4 value)
    {
        GetActiveProgram().SetMatrix4(name.ToString(), value);
    }

    /// <inheritdoc/>
    public void Draw(int vertexCount, int startVertex)
    {
        ThrowIfDisposed();
        PrimitiveType primitiveType = GetPrimitiveType();
        _context.Gl.BindVertexArray(_vertexArrayHandle);
        _context.Gl.DrawArrays((GLEnum)primitiveType, startVertex, (uint)vertexCount);
    }

    /// <inheritdoc/>
    public void DrawIndexed(int indexCount, int startIndex, int baseVertex)
    {
        ThrowIfDisposed();
        PrimitiveType primitiveType = GetPrimitiveType();
        _context.Gl.BindVertexArray(_vertexArrayHandle);
        unsafe
        {
            void* indexOffset = (void*)(startIndex * sizeof(uint));
            if (baseVertex == 0)
            {
                _context.Gl.DrawElements(primitiveType, (uint)indexCount, DrawElementsType.UnsignedInt, indexOffset);
            }
            else
            {
                _context.Gl.DrawElementsBaseVertex(primitiveType, (uint)indexCount, DrawElementsType.UnsignedInt, indexOffset, baseVertex);
            }
        }
    }

    /// <inheritdoc/>
    public void Dispatch(int groupCountX, int groupCountY, int groupCountZ)
    {
        ThrowIfDisposed();

        if (_currentPipelineState?.ComputeProgram is null)
        {
            throw new InvalidOperationException("A compute pipeline state must be bound before dispatch.");
        }

        _currentPipelineState.ComputeProgram.Dispatch((uint)groupCountX, (uint)groupCountY, (uint)groupCountZ);
    }

    /// <inheritdoc/>
    public void MemoryBarrier()
    {
        ThrowIfDisposed();
        _context.StorageMemoryBarrier();
    }

    /// <inheritdoc/>
    public void PushDebugGroup(ReadOnlySpan<char> name)
    {
        ThrowIfDisposed();
        string groupName = name.ToString();
        if (string.IsNullOrWhiteSpace(groupName))
        {
            return;
        }

        _context.Gl.PushDebugGroup(GLEnum.DebugSourceApplication, 0, (uint)groupName.Length, groupName);
    }

    /// <inheritdoc/>
    public void PopDebugGroup()
    {
        ThrowIfDisposed();
        _context.Gl.PopDebugGroup();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_vertexArrayHandle != 0)
        {
            _context.DeleteVertexArray(_vertexArrayHandle);
            _vertexArrayHandle = 0;
        }

        _boundBuffers.Clear();
        _currentPipelineState = null;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void ApplyPipelineState(OpenGlPipelineState pipelineState)
    {
        GL gl = _context.Gl;

        if (pipelineState.GraphicsProgram is not null)
        {
            pipelineState.GraphicsProgram.Use();
            gl.BindVertexArray(_vertexArrayHandle);
        }
        else
        {
            pipelineState.ComputeProgram?.Use();
        }

        if (pipelineState.CullMode == CullMode.None)
        {
            gl.Disable(EnableCap.CullFace);
        }
        else
        {
            gl.Enable(EnableCap.CullFace);
            gl.CullFace(pipelineState.CullMode == CullMode.Front ? TriangleFace.Front : TriangleFace.Back);
        }

        _context.SetFrontFace(pipelineState.FaceWinding == FaceWinding.CounterClockwise);
        gl.PolygonMode(TriangleFace.FrontAndBack, pipelineState.Wireframe ? GLEnum.Line : GLEnum.Fill);

        if (pipelineState.BlendEnabled)
        {
            gl.Enable(EnableCap.Blend);
            gl.BlendFunc(GetBlendFactor(pipelineState.SourceBlendFactor), GetBlendFactor(pipelineState.DestinationBlendFactor));
            gl.BlendEquation(GetBlendEquation(pipelineState.BlendOperation));
        }
        else
        {
            gl.Disable(EnableCap.Blend);
        }

        if (pipelineState.DepthTest)
        {
            gl.Enable(EnableCap.DepthTest);
            gl.DepthFunc(GetDepthFunction(pipelineState.DepthCompareFunc));
        }
        else
        {
            gl.Disable(EnableCap.DepthTest);
        }

        gl.DepthMask(pipelineState.DepthWrite);
    }

    private unsafe void ConfigureVertexAttribute(int slot, OpenGlBuffer buffer, OpenGlPipelineState pipelineState)
    {
        if (slot < 0 || slot >= pipelineState.VertexAttributes.Length)
        {
            return;
        }

        VertexAttribute attribute = pipelineState.VertexAttributes[slot];
        GL gl = _context.Gl;
        gl.BindVertexArray(_vertexArrayHandle);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, buffer.Handle);
        gl.EnableVertexAttribArray((uint)slot);

        if (attribute.Type is VertexAttributeType.Int32 or VertexAttributeType.UInt32 or VertexAttributeType.Int16 or VertexAttributeType.UInt16 or VertexAttributeType.Int8 or VertexAttributeType.UInt8)
        {
            gl.VertexAttribIPointer((uint)slot, attribute.ComponentCount, GetVertexAttribIntegerType(attribute.Type), (uint)attribute.StrideBytes, (void*)attribute.OffsetBytes);
            return;
        }

        gl.VertexAttribPointer((uint)slot, attribute.ComponentCount, GetVertexAttribFloatType(attribute.Type), false, (uint)attribute.StrideBytes, (void*)attribute.OffsetBytes);
    }

    private GlProgramBase GetActiveProgram()
    {
        ThrowIfDisposed();

        if (_currentPipelineState?.GraphicsProgram is not null)
        {
            return _currentPipelineState.GraphicsProgram;
        }

        if (_currentPipelineState?.ComputeProgram is not null)
        {
            return _currentPipelineState.ComputeProgram;
        }

        throw new InvalidOperationException("A pipeline state must be bound before setting uniforms.");
    }

    private PrimitiveType GetPrimitiveType()
    {
        if (_currentPipelineState is null)
        {
            throw new InvalidOperationException("A graphics pipeline state must be bound before drawing.");
        }

        return _currentPipelineState.PrimitiveTopology switch
        {
            PrimitiveTopology.Points => PrimitiveType.Points,
            PrimitiveTopology.Lines => PrimitiveType.Lines,
            PrimitiveTopology.LineStrip => PrimitiveType.LineStrip,
            PrimitiveTopology.TriangleStrip => PrimitiveType.TriangleStrip,
            _ => PrimitiveType.Triangles,
        };
    }

    private static GLEnum GetBlendEquation(BlendOp blendOperation)
    {
        return blendOperation switch
        {
            BlendOp.Subtract => GLEnum.FuncSubtract,
            BlendOp.ReverseSubtract => GLEnum.FuncReverseSubtract,
            BlendOp.Minimum => GLEnum.Min,
            BlendOp.Maximum => GLEnum.Max,
            _ => GLEnum.FuncAdd,
        };
    }

    private static BlendingFactor GetBlendFactor(BlendFactor blendFactor)
    {
        return blendFactor switch
        {
            BlendFactor.Zero => BlendingFactor.Zero,
            BlendFactor.SourceColor => BlendingFactor.SrcColor,
            BlendFactor.InverseSourceColor => BlendingFactor.OneMinusSrcColor,
            BlendFactor.SourceAlpha => BlendingFactor.SrcAlpha,
            BlendFactor.InverseSourceAlpha => BlendingFactor.OneMinusSrcAlpha,
            BlendFactor.DestinationColor => BlendingFactor.DstColor,
            BlendFactor.InverseDestinationColor => BlendingFactor.OneMinusDstColor,
            BlendFactor.DestinationAlpha => BlendingFactor.DstAlpha,
            BlendFactor.InverseDestinationAlpha => BlendingFactor.OneMinusDstAlpha,
            _ => BlendingFactor.One,
        };
    }

    private static DepthFunction GetDepthFunction(CompareFunc compareFunc)
    {
        return compareFunc switch
        {
            CompareFunc.Never => DepthFunction.Never,
            CompareFunc.Less => DepthFunction.Less,
            CompareFunc.Equal => DepthFunction.Equal,
            CompareFunc.Greater => DepthFunction.Greater,
            CompareFunc.GreaterOrEqual => DepthFunction.Gequal,
            CompareFunc.NotEqual => DepthFunction.Notequal,
            CompareFunc.Always => DepthFunction.Always,
            _ => DepthFunction.Lequal,
        };
    }

    private static VertexAttribIType GetVertexAttribIntegerType(VertexAttributeType vertexAttributeType)
    {
        return vertexAttributeType switch
        {
            VertexAttributeType.Int32 => VertexAttribIType.Int,
            VertexAttributeType.UInt32 => VertexAttribIType.UnsignedInt,
            VertexAttributeType.Int16 => VertexAttribIType.Short,
            VertexAttributeType.UInt16 => VertexAttribIType.UnsignedShort,
            VertexAttributeType.Int8 => VertexAttribIType.Byte,
            VertexAttributeType.UInt8 => VertexAttribIType.UnsignedByte,
            _ => VertexAttribIType.Int,
        };
    }

    private static VertexAttribPointerType GetVertexAttribFloatType(VertexAttributeType vertexAttributeType)
    {
        return vertexAttributeType switch
        {
            VertexAttributeType.Float16 => VertexAttribPointerType.HalfFloat,
            VertexAttributeType.Int32 => VertexAttribPointerType.Int,
            VertexAttributeType.UInt32 => VertexAttribPointerType.UnsignedInt,
            VertexAttributeType.Int16 => VertexAttribPointerType.Short,
            VertexAttributeType.UInt16 => VertexAttribPointerType.UnsignedShort,
            VertexAttributeType.Int8 => VertexAttribPointerType.Byte,
            VertexAttributeType.UInt8 => VertexAttribPointerType.UnsignedByte,
            _ => VertexAttribPointerType.Float,
        };
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}