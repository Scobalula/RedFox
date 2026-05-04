using RedFox.Graphics3D.Rendering;
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
    private const int MaxLights = 4;
    private static readonly string[] LightColorUniformNames = CreateIndexedUniformNames("LightColors", MaxLights);
    private static readonly string[] LightDirectionUniformNames = CreateIndexedUniformNames("LightDirectionsAndIntensity", MaxLights);

    private readonly Dictionary<int, OpenGlBuffer> _boundBuffers = [];
    private readonly OpenGlContext _context;
    private readonly Vector3[] _lightColors = new Vector3[MaxLights];
    private readonly Vector4[] _lightDirectionsAndIntensity = new Vector4[MaxLights];
    private OpenGlPipelineState? _currentPipelineState;
    private Vector3 _ambientColor;
    private bool _disposed;
    private int _enabledVertexAttributeCount;
    private Vector3 _fallbackLightColor = Vector3.One;
    private Vector3 _fallbackLightDirection = -Vector3.UnitY;
    private float _fallbackLightIntensity = 1.0f;
    private FaceWinding _frontFaceWinding = FaceWinding.CounterClockwise;
    private OpenGlBuffer? _indexBuffer;
    private int _lightCount;
    private Matrix4x4 _sceneAxis = Matrix4x4.Identity;
    private SkinningMode _skinningMode = SkinningMode.Linear;
    private bool _useViewBasedLighting;
    private uint _vertexArrayHandle;

    /// <inheritdoc/>
    public ulong FrameIndex { get; private set; }

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
        IncrementFrameIndex();
        _boundBuffers.Clear();
        _currentPipelineState = null;
        _ambientColor = Vector3.Zero;
        _fallbackLightDirection = -Vector3.UnitY;
        _fallbackLightColor = Vector3.One;
        _fallbackLightIntensity = 1.0f;
        _frontFaceWinding = FaceWinding.CounterClockwise;
        _indexBuffer = null;
        _lightCount = 0;
        _sceneAxis = Matrix4x4.Identity;
        _skinningMode = SkinningMode.Linear;
        _useViewBasedLighting = false;
        _context.Gl.DepthMask(true);
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
            : _context.DefaultFramebufferHandle;
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, handle);
        if (renderTarget is OpenGlRenderTarget { SampleCount: > 1 })
        {
            gl.Enable(EnableCap.Multisample);
        }
    }

    /// <inheritdoc/>
    public void ClearRenderTarget(float red, float green, float blue, float alpha, float depth)
    {
        ThrowIfDisposed();

        GL gl = _context.Gl;
        bool restoreDepthMask = _currentPipelineState?.GraphicsProgram is not null
            ? _currentPipelineState.DepthWrite
            : true;
        gl.DepthMask(true);
        gl.ClearColor(red, green, blue, alpha);
        gl.ClearDepth(depth);
        gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));
        gl.DepthMask(restoreDepthMask);
    }

    /// <inheritdoc/>
    public void ResolveRenderTarget(IGpuRenderTarget source, IGpuRenderTarget? destination)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(source);

        OpenGlRenderTarget openGlSource = source as OpenGlRenderTarget
            ?? throw new InvalidOperationException($"Expected {nameof(OpenGlRenderTarget)} for source render target.");
        OpenGlRenderTarget? openGlDestination = destination as OpenGlRenderTarget;
        uint destinationHandle = openGlDestination?.Handle ?? _context.DefaultFramebufferHandle;
        int destinationWidth = openGlDestination?.Width ?? openGlSource.Width;
        int destinationHeight = openGlDestination?.Height ?? openGlSource.Height;

        GL gl = _context.Gl;
        gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, openGlSource.Handle);
        gl.ReadBuffer(GLEnum.ColorAttachment0);
        EnsureFramebufferComplete(gl, FramebufferTarget.ReadFramebuffer, "read");

        gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, destinationHandle);
        EnsureFramebufferComplete(gl, FramebufferTarget.DrawFramebuffer, "draw");

        gl.BlitFramebuffer(
            0,
            0,
            openGlSource.Width,
            openGlSource.Height,
            0,
            0,
            destinationWidth,
            destinationHeight,
            (uint)ClearBufferMask.ColorBufferBit,
            GLEnum.Nearest);
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, destinationHandle);
    }

    /// <inheritdoc/>
    public void SetPipelineState(IGpuPipelineState pipelineState)
    {
        ThrowIfDisposed();

        OpenGlPipelineState nextPipelineState = pipelineState as OpenGlPipelineState
            ?? throw new InvalidOperationException($"Expected {nameof(OpenGlPipelineState)}.");
        if (ReferenceEquals(_currentPipelineState, nextPipelineState))
        {
            return;
        }

        _currentPipelineState = nextPipelineState;
        ApplyPipelineState(_currentPipelineState);
    }

    /// <inheritdoc/>
    public void SetSceneAxis(Matrix4x4 sceneAxis)
    {
        ThrowIfDisposed();
        _sceneAxis = sceneAxis;
    }

    /// <inheritdoc/>
    public void SetFrontFaceWinding(FaceWinding faceWinding)
    {
        ThrowIfDisposed();
        _frontFaceWinding = faceWinding;
    }

    /// <inheritdoc/>
    public void SetAmbientColor(Vector3 ambientColor)
    {
        ThrowIfDisposed();
        _ambientColor = ambientColor;
        ApplySceneStateToCurrentProgram();
    }

    /// <inheritdoc/>
    public void SetUseViewBasedLighting(bool enabled)
    {
        ThrowIfDisposed();
        _useViewBasedLighting = enabled;
        ApplySceneStateToCurrentProgram();
    }

    /// <inheritdoc/>
    public void SetSkinningMode(SkinningMode skinningMode)
    {
        ThrowIfDisposed();
        _skinningMode = skinningMode;

        if (_currentPipelineState?.ComputeProgram is GlComputeProgram computeProgram)
        {
            computeProgram.SetInt("SkinningMode", (int)_skinningMode);
        }

        ApplySceneStateToCurrentProgram();
    }

    /// <inheritdoc/>
    public void ResetLights(Vector3 fallbackDirection, Vector3 fallbackColor, float fallbackIntensity)
    {
        ThrowIfDisposed();
        _fallbackLightDirection = fallbackDirection;
        _fallbackLightColor = fallbackColor;
        _fallbackLightIntensity = fallbackIntensity;
        _lightCount = 0;
        Array.Clear(_lightDirectionsAndIntensity);
        Array.Clear(_lightColors);
        ApplySceneStateToCurrentProgram();
    }

    /// <inheritdoc/>
    public void AppendLight(Vector3 direction, Vector3 color, float intensity)
    {
        ThrowIfDisposed();

        if (_lightCount >= MaxLights)
        {
            return;
        }

        _lightDirectionsAndIntensity[_lightCount] = new Vector4(TransformDirection(direction), intensity);
        _lightColors[_lightCount] = color;
        _lightCount++;
        ApplySceneStateToCurrentProgram();
    }

    /// <inheritdoc/>
    public void BindBuffer(int slot, IGpuBuffer buffer)
    {
        ThrowIfDisposed();

        OpenGlBuffer openGlBuffer = buffer as OpenGlBuffer
            ?? throw new InvalidOperationException($"Expected {nameof(OpenGlBuffer)}.");

        if (openGlBuffer.Usage.HasFlag(BufferUsage.Sampled))
        {
            if (openGlBuffer.SampledTextureHandle == 0)
            {
                throw new InvalidOperationException("The OpenGL sampled buffer does not have a texture-buffer view.");
            }

            _context.Gl.ActiveTexture(TextureUnit.Texture0 + slot);
            _context.Gl.BindTexture(openGlBuffer.SampledTextureTarget, openGlBuffer.SampledTextureHandle);
            return;
        }

        _boundBuffers[slot] = openGlBuffer;

        if (openGlBuffer.Usage.HasFlag(BufferUsage.Index))
        {
            BindIndexBuffer(openGlBuffer);
            return;
        }

        if (_currentPipelineState is null)
        {
            return;
        }

        if (_currentPipelineState.IsCompute)
        {
            if (openGlBuffer.Usage.HasFlag(BufferUsage.Uniform))
            {
                _context.Gl.BindBufferBase(BufferTargetARB.UniformBuffer, (uint)slot, openGlBuffer.Handle);
            }
            else
            {
                _context.Gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, (uint)slot, openGlBuffer.Handle);
            }

            return;
        }

        if (openGlBuffer.Usage.HasFlag(BufferUsage.Vertex) && slot < _currentPipelineState.VertexAttributes.Length)
        {
            ConfigureVertexAttribute(slot, openGlBuffer, _currentPipelineState);
            return;
        }

        if (openGlBuffer.Usage.HasFlag(BufferUsage.Uniform))
        {
            _context.Gl.BindBufferBase(BufferTargetARB.UniformBuffer, (uint)slot, openGlBuffer.Handle);
            return;
        }

        if (openGlBuffer.Usage.HasFlag(BufferUsage.ShaderStorage) || openGlBuffer.Usage.HasFlag(BufferUsage.Structured))
        {
            _context.Gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, (uint)slot, openGlBuffer.Handle);
        }
    }

    /// <inheritdoc/>
    public void BindIndexBuffer(IGpuBuffer buffer)
    {
        ThrowIfDisposed();

        OpenGlBuffer openGlBuffer = buffer as OpenGlBuffer
            ?? throw new InvalidOperationException($"Expected {nameof(OpenGlBuffer)}.");
        if (!openGlBuffer.Usage.HasFlag(BufferUsage.Index))
        {
            throw new InvalidOperationException("Expected an OpenGL index buffer.");
        }

        _indexBuffer = openGlBuffer;
        _context.Gl.BindVertexArray(_vertexArrayHandle);
        _context.Gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, openGlBuffer.Handle);
    }

    /// <inheritdoc/>
    public void BindTexture(int slot, IGpuTexture texture)
    {
        ThrowIfDisposed();

        OpenGlTexture openGlTexture = texture as OpenGlTexture
            ?? throw new InvalidOperationException($"Expected {nameof(OpenGlTexture)}.");
        if (openGlTexture.IsRenderbuffer)
        {
            throw new InvalidOperationException("OpenGL renderbuffers cannot be bound as sampled textures.");
        }

        GL gl = _context.Gl;
        gl.ActiveTexture(TextureUnit.Texture0 + slot);
        gl.BindTexture(openGlTexture.Target, openGlTexture.Handle);
    }

    /// <inheritdoc/>
    public void SetUniformInt(string name, int value)
    {
        GetActiveProgram().SetInt(name, value);
    }

    /// <inheritdoc/>
    public void SetUniformFloat(string name, float value)
    {
        GetActiveProgram().SetFloat(name, value);
    }

    /// <inheritdoc/>
    public void SetUniformVector2(string name, Vector2 value)
    {
        GetActiveProgram().SetVector2(name, value);
    }

    /// <inheritdoc/>
    public void SetUniformVector3(string name, Vector3 value)
    {
        GetActiveProgram().SetVector3(name, value);
    }

    /// <inheritdoc/>
    public void SetUniformVector4(string name, Vector4 value)
    {
        GetActiveProgram().SetVector4(name, value);
    }

    /// <inheritdoc/>
    public void SetUniformMatrix4x4(string name, Matrix4x4 value)
    {
        GetActiveProgram().SetMatrix4(name, value);
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
        GpuBufferElementType indexElementType = _indexBuffer?.ElementType ?? GpuBufferElementType.Unknown;
        DrawElementsType drawElementsType = GetDrawElementsType(indexElementType);
        int indexElementSizeBytes = GetIndexElementSizeBytes(indexElementType);
        unsafe
        {
            void* indexOffset = (void*)(startIndex * indexElementSizeBytes);
            if (baseVertex == 0)
            {
                _context.Gl.DrawElements(primitiveType, (uint)indexCount, drawElementsType, indexOffset);
            }
            else
            {
                _context.Gl.DrawElementsBaseVertex(primitiveType, (uint)indexCount, drawElementsType, indexOffset, baseVertex);
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
    public void PushDebugGroup(string name)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        _context.Gl.PushDebugGroup(GLEnum.DebugSourceApplication, 0, (uint)name.Length, name);
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
            DisableUnusedVertexAttributes(pipelineState.VertexAttributes.Length);
        }
        else
        {
            pipelineState.ComputeProgram?.Use();
            pipelineState.ComputeProgram?.SetInt("SkinningMode", (int)_skinningMode);
        }

        ApplySceneStateToCurrentProgram();

        if (pipelineState.CullMode == CullMode.None)
        {
            gl.Disable(EnableCap.CullFace);
        }
        else
        {
            gl.Enable(EnableCap.CullFace);
            gl.CullFace(pipelineState.CullMode == CullMode.Front ? TriangleFace.Front : TriangleFace.Back);
        }

        _context.SetFrontFace(_frontFaceWinding == FaceWinding.CounterClockwise);

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
        _enabledVertexAttributeCount = Math.Max(_enabledVertexAttributeCount, slot + 1);

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

    private void ApplySceneStateToCurrentProgram()
    {
        if (_currentPipelineState?.GraphicsProgram is not GlShaderProgram graphicsProgram)
        {
            return;
        }

        graphicsProgram.SetVector3("AmbientColor", _ambientColor);
        graphicsProgram.SetInt("SkinningMode", (int)_skinningMode);
        graphicsProgram.SetInt("UseViewBasedLighting", _useViewBasedLighting ? 1 : 0);

        int appliedLightCount = _lightCount;
        if (appliedLightCount == 0)
        {
            _lightDirectionsAndIntensity[0] = new Vector4(TransformDirection(_fallbackLightDirection), _fallbackLightIntensity);
            _lightColors[0] = _fallbackLightColor;
            appliedLightCount = 1;
        }

        graphicsProgram.SetInt("LightCount", appliedLightCount);
        for (int i = 0; i < MaxLights; i++)
        {
            graphicsProgram.SetVector4(LightDirectionUniformNames[i], _lightDirectionsAndIntensity[i]);
            graphicsProgram.SetVector3(LightColorUniformNames[i], _lightColors[i]);
        }
    }

    private void IncrementFrameIndex()
    {
        unchecked
        {
            FrameIndex++;
            if (FrameIndex == 0)
            {
                FrameIndex = 1;
            }
        }
    }

    private static string[] CreateIndexedUniformNames(string baseName, int count)
    {
        string[] names = new string[count];
        for (int i = 0; i < names.Length; i++)
        {
            names[i] = $"{baseName}[{i}]";
        }

        return names;
    }

    private static void EnsureFramebufferComplete(GL gl, FramebufferTarget target, string label)
    {
        GLEnum status = gl.CheckFramebufferStatus(target);
        if (status != GLEnum.FramebufferComplete)
        {
            throw new InvalidOperationException($"OpenGL {label} framebuffer is incomplete: {status}.");
        }
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

    private Vector3 TransformDirection(Vector3 direction)
    {
        Vector3 safeDirection = direction;
        if (safeDirection.LengthSquared() < 1e-10f)
        {
            safeDirection = -Vector3.UnitY;
        }

        Vector3 transformed = Vector3.TransformNormal(Vector3.Normalize(safeDirection), _sceneAxis);
        if (transformed.LengthSquared() < 1e-10f)
        {
            return -Vector3.UnitY;
        }

        return Vector3.Normalize(transformed);
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

    private static DrawElementsType GetDrawElementsType(GpuBufferElementType elementType)
    {
        return elementType switch
        {
            GpuBufferElementType.UInt8 => DrawElementsType.UnsignedByte,
            GpuBufferElementType.UInt16 => DrawElementsType.UnsignedShort,
            GpuBufferElementType.Unknown or GpuBufferElementType.UInt32 => DrawElementsType.UnsignedInt,
            _ => throw new NotSupportedException($"OpenGL index buffers do not support '{elementType}' elements."),
        };
    }

    private static int GetIndexElementSizeBytes(GpuBufferElementType elementType)
    {
        return elementType switch
        {
            GpuBufferElementType.UInt8 => sizeof(byte),
            GpuBufferElementType.UInt16 => sizeof(ushort),
            GpuBufferElementType.Unknown or GpuBufferElementType.UInt32 => sizeof(uint),
            _ => throw new NotSupportedException($"OpenGL index buffers do not support '{elementType}' elements."),
        };
    }

    private void DisableUnusedVertexAttributes(int requiredAttributeCount)
    {
        GL gl = _context.Gl;
        for (int slot = requiredAttributeCount; slot < _enabledVertexAttributeCount; slot++)
        {
            gl.DisableVertexAttribArray((uint)slot);
        }

        _enabledVertexAttributeCount = requiredAttributeCount;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}