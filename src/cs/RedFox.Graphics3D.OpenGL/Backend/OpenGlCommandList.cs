using RedFox.Graphics3D.Rendering;
using RedFox.Graphics3D.OpenGL.Resources;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace RedFox.Graphics3D.OpenGL;

/// <summary>
/// Represents the concrete OpenGL immediate-mode command list implementation.
/// </summary>
internal sealed class OpenGlCommandList : ICommandList, IDisposable
{
    private const int MaxLights = 4;
    private const int EnvironmentMapSlot = 31;
    private const int ShadowMapSlot = 30;
    private const int GlobalTextureSlot = 29;
    private const int MaxGlobalTextures = 4;
    private const int MaxVertexInputs = 16;
    private static readonly string[] LightColorUniformNames = CreateIndexedUniformNames("LightColors", MaxLights);
    private static readonly string[] LightDirectionUniformNames = CreateIndexedUniformNames("LightDirectionsAndIntensity", MaxLights);
    private static readonly string[] EnvironmentLightDirectionUniformNames = CreateIndexedUniformNames("EnvironmentLightDirections", MaxLights);
    private static readonly string[] EnvironmentLightPowerUniformNames = CreateIndexedUniformNames("EnvironmentLightPowers", MaxLights);
    private static readonly string[] ShadowMatrixUniformNames = CreateIndexedUniformNames("ShadowMatrices", OpenGlShadowMapArray.MaxLayers);
    private static readonly string[] ShadowFilterRadiusUniformNames = CreateIndexedUniformNames("ShadowFilterRadii", OpenGlShadowMapArray.MaxLayers);

    private readonly List<string> _globalTextureNames = [];
    private readonly List<string> _globalTextureFlagNames = [];
    private readonly List<IGpuTexture?> _globalTextures = [];
    private readonly OpenGlContext _context;
    private readonly Vector3[] _lightColors = new Vector3[MaxLights];
    private readonly Vector4[] _lightDirectionsAndIntensity = new Vector4[MaxLights];
    private readonly Vector4[] _environmentLightDirections = new Vector4[MaxLights];
    private readonly Vector3[] _environmentLightPowers = new Vector3[MaxLights];
    private readonly Matrix4x4[] _shadowMatrices = new Matrix4x4[OpenGlShadowMapArray.MaxLayers];
    private readonly Vector2[] _shadowFilterRadii = new Vector2[OpenGlShadowMapArray.MaxLayers];
    private readonly OpenGlShadowMapArray _shadowMaps;
    private readonly OpenGlBuffer?[] _vertexInputs = new OpenGlBuffer?[MaxVertexInputs];
    private OpenGlPipelineState? _currentPipelineState;
    private Vector3 _ambientColor;
    private bool _disposed;
    private float _environmentIntensity;
    private IGpuTexture? _environmentMap;
    private int _environmentMipLevels;
    private int _environmentLightCount;
    private Vector3 _fallbackLightColor = Vector3.One;
    private Vector3 _fallbackLightDirection = -Vector3.UnitY;
    private float _fallbackLightIntensity = 1.0f;
    private FaceWinding _frontFaceWinding = FaceWinding.Clockwise;
    private OpenGlBuffer? _indexBuffer;
    private int _lightCount;
    private Matrix4x4 _sceneAxis = Matrix4x4.Identity;
    private int _shadowCount;
    private int _shadowFilterTaps;
    private float _shadowStrength;
    private int _shadowPassIndex = -1;
    private SkinningMode _skinningMode = SkinningMode.Linear;
    private bool _useViewBasedLighting;
    private bool _useFlatShading;
    private uint _vertexArrayHandle;
    private int _vertexInputCount;

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
        _shadowMaps = new OpenGlShadowMapArray(context);
    }

    /// <inheritdoc/>
    public void Reset()
    {
        ThrowIfDisposed();
        IncrementFrameIndex();
        _currentPipelineState = null;
        _ambientColor = Vector3.Zero;
        _fallbackLightDirection = -Vector3.UnitY;
        _fallbackLightColor = Vector3.One;
        _fallbackLightIntensity = 1.0f;
        _frontFaceWinding = FaceWinding.Clockwise;
        ClearVertexInputs();
        _lightCount = 0;
        _environmentLightCount = 0;
        _shadowCount = 0;
        _shadowPassIndex = -1;
        _sceneAxis = Matrix4x4.Identity;
        _skinningMode = SkinningMode.Linear;
        _useViewBasedLighting = false;
        _useFlatShading = false;
        _environmentMap = null;
        _context.Gl.DepthMask(true);

        for (int i = 0; i < _globalTextureNames.Count; i++)
        {
            SetGlobalTexture(_globalTextureNames[i], null);
        }
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

        gl.BlitFramebuffer(0, 0, openGlSource.Width, openGlSource.Height, 0, 0, destinationWidth, destinationHeight, (uint)ClearBufferMask.ColorBufferBit, GLEnum.Nearest);
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
    public void SetUseFlatShading(bool enabled)
    {
        ThrowIfDisposed();
        _useFlatShading = enabled;
        ApplySceneStateToCurrentProgram();
    }

    /// <inheritdoc/>
    public void SetEnvironmentMap(IGpuTexture? texture, int mipLevels, float intensity)
    {
        ThrowIfDisposed();
        _environmentMap = texture;
        _environmentMipLevels = mipLevels;
        _environmentIntensity = intensity;
        ApplySceneStateToCurrentProgram();
    }

    public void SetGlobalTexture(string name, IGpuTexture? texture)
    {
        ThrowIfDisposed();

        int index = _globalTextureNames.IndexOf(name);
        if (index < 0)
        {
            if (_globalTextureNames.Count == MaxGlobalTextures)
            {
                throw new InvalidOperationException($"No more than {MaxGlobalTextures} global textures can be set.");
            }

            index = _globalTextureNames.Count;
            _globalTextureNames.Add(name);
            _globalTextureFlagNames.Add($"Has{name}");
            _globalTextures.Add(null);
        }

        _globalTextures[index] = texture;

        if (texture is null)
        {
            _context.Gl.ActiveTexture(TextureUnit.Texture0 + GlobalTextureSlot - index);
            _context.Gl.BindTexture(TextureTarget.Texture2D, 0);
        }

        ApplySceneStateToCurrentProgram();
    }

    /// <inheritdoc/>
    public SkinningMode SkinningMode => _skinningMode;

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
        _environmentLightCount = 0;
        Array.Clear(_lightDirectionsAndIntensity);
        Array.Clear(_lightColors);
        Array.Clear(_environmentLightDirections);
        Array.Clear(_environmentLightPowers);
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

    public void AppendEnvironmentLight(Vector3 direction, Vector3 power, Vector2 halfAngles)
    {
        ThrowIfDisposed();

        if (_environmentLightCount >= MaxLights)
        {
            return;
        }

        _environmentLightDirections[_environmentLightCount] = new Vector4(direction, MathF.Sqrt(halfAngles.X * halfAngles.Y));
        _environmentLightPowers[_environmentLightCount] = power;
        _environmentLightCount++;
        ApplySceneStateToCurrentProgram();
    }

    public bool BeginShadowMap(int lightIndex, int mapSize, Matrix4x4 shadowMatrix, Vector2 filterRadius)
    {
        ThrowIfDisposed();

        if (lightIndex >= _environmentLightCount || !_shadowMaps.Begin(lightIndex, _environmentLightCount, mapSize, ShadowMapSlot))
        {
            return false;
        }

        _shadowMatrices[lightIndex] = shadowMatrix;
        _shadowFilterRadii[lightIndex] = filterRadius;
        _shadowPassIndex = lightIndex;
        _currentPipelineState = null;
        return true;
    }

    public void EndShadowMaps(int shadowCount, int filterTaps, float strength)
    {
        ThrowIfDisposed();

        if (_shadowPassIndex >= 0)
        {
            _shadowMaps.End(ShadowMapSlot);
        }

        _shadowPassIndex = -1;
        _shadowCount		= shadowCount;
        _shadowFilterTaps	= filterTaps;
        _shadowStrength		= strength;
        _currentPipelineState = null;
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
            _vertexInputs[slot] = openGlBuffer;
            _vertexInputCount = Math.Max(_vertexInputCount, slot + 1);
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
        if (_shadowPassIndex >= 0 && !CanCastShadow())
        {
            ClearVertexInputs();
            return;
        }

        PrimitiveType primitiveType = GetPrimitiveType();
        BindVertexInputs(_currentPipelineState);
        _context.Gl.DrawArrays((GLEnum)primitiveType, startVertex, (uint)vertexCount);
        ClearVertexInputs();
    }

    /// <inheritdoc/>
    public void DrawIndexed(int indexCount, int startIndex, int baseVertex)
    {
        ThrowIfDisposed();
        if (_shadowPassIndex >= 0 && !CanCastShadow())
        {
            ClearVertexInputs();
            return;
        }

        PrimitiveType primitiveType = GetPrimitiveType();
        BindVertexInputs(_currentPipelineState);
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

        ClearVertexInputs();
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

        _shadowMaps.Dispose();
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

        if (_shadowPassIndex >= 0)
        {
            gl.Disable(EnableCap.Blend);
            gl.Enable(EnableCap.DepthTest);
            gl.DepthFunc(DepthFunction.Lequal);
            gl.DepthMask(true);
        }
    }

    private bool CanCastShadow()
    {
        return _currentPipelineState is { BlendEnabled: false, GraphicsProgram: { } program } && program.HasUniform("ShadowPass");
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
        graphicsProgram.SetInt("UseFlatShading", _useFlatShading ? 1 : 0);
        graphicsProgram.SetInt("HasEnvironmentMap", _environmentMap is null ? 0 : 1);

        if (_environmentMap is not null)
        {
            BindTexture(EnvironmentMapSlot, _environmentMap);
            graphicsProgram.SetInt("EnvironmentMap", EnvironmentMapSlot);
            graphicsProgram.SetFloat("EnvironmentMipLevels", _environmentMipLevels);
            graphicsProgram.SetFloat("EnvironmentIntensity", _environmentIntensity);
        }

        for (int i = 0; i < _globalTextures.Count; i++)
        {
            if (_globalTextures[i] is { } globalTexture)
            {
                BindTexture(GlobalTextureSlot - i, globalTexture);
            }

            graphicsProgram.SetInt(_globalTextureNames[i], GlobalTextureSlot - i);
            graphicsProgram.SetInt(_globalTextureFlagNames[i], _globalTextures[i] is null ? 0 : 1);
        }

        int appliedLightCount = _lightCount;
        if (appliedLightCount == 0 && _environmentLightCount == 0)
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
            graphicsProgram.SetVector4(EnvironmentLightDirectionUniformNames[i], _environmentLightDirections[i]);
            graphicsProgram.SetVector3(EnvironmentLightPowerUniformNames[i], _environmentLightPowers[i]);
        }

        graphicsProgram.SetInt("EnvironmentLightCount", _environmentLightCount);

        bool shadowPass = _shadowPassIndex >= 0;
        graphicsProgram.SetInt("ShadowMap", ShadowMapSlot);
        graphicsProgram.SetInt("ShadowPass", shadowPass ? 1 : 0);
        graphicsProgram.SetInt("AlphaToCoverage", !shadowPass && _currentPipelineState.AlphaToCoverage ? 1 : 0);
        graphicsProgram.SetInt("ShadowCount", shadowPass ? 0 : _shadowCount);

        if (!shadowPass && _shadowCount > 0)
        {
            _context.Gl.ActiveTexture(TextureUnit.Texture0 + ShadowMapSlot);
            _context.Gl.BindTexture(TextureTarget.Texture2DArray, _shadowMaps.Texture);
            graphicsProgram.SetFloat("ShadowMapSize", _shadowMaps.Size);
            graphicsProgram.SetInt("ShadowFilterTaps", _shadowFilterTaps);
            graphicsProgram.SetFloat("ShadowStrength", _shadowStrength);

            for (int i = 0; i < _shadowCount; i++)
            {
                graphicsProgram.SetMatrix4(ShadowMatrixUniformNames[i], _shadowMatrices[i]);
                graphicsProgram.SetVector2(ShadowFilterRadiusUniformNames[i], _shadowFilterRadii[i]);
            }
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

    [MemberNotNull(nameof(_currentPipelineState))]
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

    private void BindVertexInputs(OpenGlPipelineState pipelineState)
    {
        OpenGlBuffer? owner = _indexBuffer;
        for (int slot = 0; owner is null && slot < _vertexInputCount; slot++)
        {
            owner = _vertexInputs[slot];
        }

        if (owner is null)
        {
            _context.Gl.BindVertexArray(_vertexArrayHandle);
            return;
        }

        owner.VertexArray ??= new OpenGlVertexArray(_context);
        owner.VertexArray.Bind(pipelineState, _vertexInputs.AsSpan(0, _vertexInputCount), _indexBuffer);
    }

    private void ClearVertexInputs()
    {
        Array.Clear(_vertexInputs, 0, _vertexInputCount);
        _vertexInputCount = 0;
        _indexBuffer = null;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}