using System.Numerics;
using System.Runtime.InteropServices;
using RedFox.Graphics3D.Rendering;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;

namespace RedFox.Graphics3D.D3D11;

/// <summary>
/// Represents the Direct3D 11 immediate command list implementation.
/// </summary>
public sealed unsafe class D3D11CommandList : ICommandList, IDisposable
{
    private const int ConstantBufferStageCount = 3;
    private const int ComputeFirstOutputBufferSlot = 5;
    private const int ComputeOutputBufferCount = 2;
    private const int ComputeShaderResourceCount = 5;
    private const int MaxConstantBufferSlots = 14;
    private const int MaxLights = 4;

    private readonly D3D11ConstantBufferSlot[] _constantBufferSlots = new D3D11ConstantBufferSlot[MaxConstantBufferSlots * ConstantBufferStageCount];
    private readonly D3D11Context _context;
    private readonly Vector3[] _lightColors = new Vector3[MaxLights];
    private readonly Vector4[] _lightDirectionsAndIntensity = new Vector4[MaxLights];
    private readonly Dictionary<string, D3D11UniformValue> _uniformValues = new(StringComparer.Ordinal);
    private D3D11PipelineState? _currentPipelineState;
    private ID3D11DepthStencilView* _currentDepthStencilView;
    private ID3D11RenderTargetView* _currentRenderTargetView;
    private bool _disposed;
    private Vector3 _fallbackLightColor = Vector3.One;
    private Vector3 _fallbackLightDirection = -Vector3.UnitY;
    private float _fallbackLightIntensity = 1.0f;
    private FaceWinding _frontFaceWinding = FaceWinding.CounterClockwise;
    private int _lightCount;
    private Matrix4x4 _sceneAxis = Matrix4x4.Identity;

    /// <inheritdoc/>
    public ulong FrameIndex { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="D3D11CommandList"/> class.
    /// </summary>
    /// <param name="context">The owning Direct3D 11 context wrapper.</param>
    internal D3D11CommandList(D3D11Context context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _currentRenderTargetView = context.DefaultRenderTargetView;
        _currentDepthStencilView = context.DefaultDepthStencilView;
    }

    /// <inheritdoc/>
    public void Reset()
    {
        ThrowIfDisposed();
        IncrementFrameIndex();
        _currentPipelineState = null;
        _fallbackLightDirection = -Vector3.UnitY;
        _fallbackLightColor = Vector3.One;
        _fallbackLightIntensity = 1.0f;
        _frontFaceWinding = FaceWinding.CounterClockwise;
        _lightCount = 0;
        _sceneAxis = Matrix4x4.Identity;
        _uniformValues.Clear();
        Array.Clear(_lightDirectionsAndIntensity);
        Array.Clear(_lightColors);

        SetUniformMatrix4x4("Model", Matrix4x4.Identity);
        SetUniformMatrix4x4("SceneAxis", Matrix4x4.Identity);
        SetUniformMatrix4x4("View", Matrix4x4.Identity);
        SetUniformMatrix4x4("Projection", Matrix4x4.Identity);
        SetUniformMatrix4x4("InverseView", Matrix4x4.Identity);
        SetUniformMatrix4x4("InverseProjection", Matrix4x4.Identity);
        SetUniformVector2("ViewportSize", Vector2.One);
        SetUniformVector3("AmbientColor", Vector3.Zero);
        SetUniformVector3("CameraPosition", Vector3.Zero);
        SetUniformVector4("BaseColor", Vector4.One);
        SetUniformFloat("LineHalfWidthPx", 0.5f);
        SetUniformFloat("FadeStartDistance", 0.0f);
        SetUniformFloat("FadeEndDistance", 0.0f);
        SetUniformFloat("GridCellSize", 2.0f);
        SetUniformFloat("GridMajorStep", 10.0f);
        SetUniformFloat("GridMinPixelsBetweenCells", 2.5f);
        SetUniformFloat("GridLineWidth", 1.1f);
        SetUniformFloat("MaterialSpecularStrength", 0.28f);
        SetUniformFloat("MaterialSpecularPower", 32.0f);
        SetUniformVector4("GridMinorColor", new Vector4(0.34f, 0.38f, 0.45f, 0.48f));
        SetUniformVector4("GridMajorColor", new Vector4(0.52f, 0.58f, 0.68f, 0.66f));
        SetUniformVector4("GridAxisXColor", new Vector4(0.9f, 0.28f, 0.25f, 0.82f));
        SetUniformVector4("GridAxisZColor", new Vector4(0.25f, 0.45f, 0.9f, 0.82f));
        SetUniformInt("SkinningMode", (int)SkinningMode.Linear);
        SetUniformInt("VertexCount", 0);
        SetUniformInt("SkinInfluenceCount", 0);
        SetUniformInt("UseViewBasedLighting", 0);
        ApplyLightUniforms();
        SetRenderTarget(null);
    }

    /// <inheritdoc/>
    public void SetViewport(int width, int height)
    {
        ThrowIfDisposed();
        int safeWidth = Math.Max(1, width);
        int safeHeight = Math.Max(1, height);
        SetUniformVector2("ViewportSize", new Vector2(safeWidth, safeHeight));
        Viewport viewport = new()
        {
            TopLeftX = 0.0f,
            TopLeftY = 0.0f,
            Width = safeWidth,
            Height = safeHeight,
            MinDepth = 0.0f,
            MaxDepth = 1.0f,
        };
        _context.DeviceContext.Get().RSSetViewports(1, ref viewport);
    }

    /// <inheritdoc/>
    public void SetRenderTarget(IGpuRenderTarget? renderTarget)
    {
        ThrowIfDisposed();
        if (renderTarget is D3D11RenderTarget d3dRenderTarget)
        {
            _currentRenderTargetView = d3dRenderTarget.RenderTargetView;
            _currentDepthStencilView = d3dRenderTarget.DepthStencilView;
        }
        else if (renderTarget is null)
        {
            _currentRenderTargetView = _context.DefaultRenderTargetView;
            _currentDepthStencilView = _context.DefaultDepthStencilView;
        }
        else
        {
            throw new InvalidOperationException($"Expected {nameof(D3D11RenderTarget)}.");
        }

        ID3D11RenderTargetView* renderTargetView = _currentRenderTargetView;
        _context.DeviceContext.Get().OMSetRenderTargets(1, &renderTargetView, _currentDepthStencilView);
    }

    /// <inheritdoc/>
    public void ClearRenderTarget(float red, float green, float blue, float alpha, float depth)
    {
        ThrowIfDisposed();
        if (_currentRenderTargetView is not null)
        {
            Span<float> color = stackalloc float[4];
            color[0] = red;
            color[1] = green;
            color[2] = blue;
            color[3] = alpha;
            fixed (float* colorPointer = color)
            {
                _context.DeviceContext.Get().ClearRenderTargetView(_currentRenderTargetView, colorPointer);
            }
        }

        if (_currentDepthStencilView is not null)
        {
            _context.DeviceContext.Get().ClearDepthStencilView(_currentDepthStencilView, (uint)ClearFlag.Depth, depth, 0);
        }
    }

    /// <inheritdoc/>
    public void ResolveRenderTarget(IGpuRenderTarget source, IGpuRenderTarget? destination)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(source);

        D3D11RenderTarget d3dSource = source as D3D11RenderTarget
            ?? throw new InvalidOperationException($"Expected {nameof(D3D11RenderTarget)} for source render target.");
        D3D11RenderTarget? d3dDestination = destination as D3D11RenderTarget;
        ID3D11Texture2D* destinationTexture = d3dDestination is not null
            ? d3dDestination.ColorTexture.Handle
            : _context.DefaultBackBuffer;
        if (destinationTexture is null)
        {
            throw new InvalidOperationException("The D3D11 resolve destination is not available.");
        }

        ID3D11RenderTargetView* nullRenderTarget = null;
        _context.DeviceContext.Get().OMSetRenderTargets(1, &nullRenderTarget, null);

        ID3D11Resource* destinationResource = (ID3D11Resource*)destinationTexture;
        ID3D11Resource* sourceResource = (ID3D11Resource*)d3dSource.ColorTexture.Handle;
        if (d3dSource.ColorTexture.SampleCount > 1)
        {
            Format resolveFormat = D3D11Helpers.GetDxgiFormat(d3dSource.ColorTexture.Format);
            _context.DeviceContext.Get().ResolveSubresource(destinationResource, 0, sourceResource, 0, resolveFormat);
        }
        else
        {
            _context.DeviceContext.Get().CopyResource(destinationResource, sourceResource);
        }

        SetRenderTarget(destination);
    }

    /// <inheritdoc/>
    public void SetPipelineState(IGpuPipelineState pipelineState)
    {
        ThrowIfDisposed();
        D3D11PipelineState nextPipelineState = pipelineState as D3D11PipelineState
            ?? throw new InvalidOperationException($"Expected {nameof(D3D11PipelineState)}.");
        if (ReferenceEquals(_currentPipelineState, nextPipelineState))
        {
            return;
        }

        _currentPipelineState = nextPipelineState;
        if (_currentPipelineState.IsCompute)
        {
            ApplyComputePipelineState(_currentPipelineState);
            return;
        }

        ApplyPipelineState(_currentPipelineState);
    }

    /// <inheritdoc/>
    public void SetSceneAxis(Matrix4x4 sceneAxis)
    {
        ThrowIfDisposed();
        _sceneAxis = sceneAxis;
        SetUniformMatrix4x4("SceneAxis", sceneAxis);
    }

    /// <inheritdoc/>
    public void SetFrontFaceWinding(FaceWinding faceWinding)
    {
        ThrowIfDisposed();
        _frontFaceWinding = faceWinding;
        if (_currentPipelineState is not null)
        {
            _context.DeviceContext.Get().RSSetState(_currentPipelineState.GetRasterizerState(_frontFaceWinding));
        }
    }

    /// <inheritdoc/>
    public void SetAmbientColor(Vector3 ambientColor)
    {
        ThrowIfDisposed();
        SetUniformVector3("AmbientColor", ambientColor);
    }

    /// <inheritdoc/>
    public void SetUseViewBasedLighting(bool enabled)
    {
        ThrowIfDisposed();
        SetUniformInt("UseViewBasedLighting", enabled ? 1 : 0);
    }

    /// <inheritdoc/>
    public void SetSkinningMode(SkinningMode skinningMode)
    {
        ThrowIfDisposed();
        SetUniformInt("SkinningMode", (int)skinningMode);
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
        ApplyLightUniforms();
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
        ApplyLightUniforms();
    }

    /// <inheritdoc/>
    public void BindBuffer(int slot, IGpuBuffer buffer)
    {
        ThrowIfDisposed();
        D3D11Buffer d3dBuffer = buffer as D3D11Buffer
            ?? throw new InvalidOperationException($"Expected {nameof(D3D11Buffer)}.");

        if (_currentPipelineState?.IsCompute == true && d3dBuffer.Usage.HasFlag(BufferUsage.ShaderStorage))
        {
            BindComputeBuffer(slot, d3dBuffer);
            return;
        }

        if (d3dBuffer.Usage.HasFlag(BufferUsage.Index))
        {
            _context.DeviceContext.Get().IASetIndexBuffer(d3dBuffer.Handle, Format.FormatR32Uint, 0);
            return;
        }

        if (d3dBuffer.Usage.HasFlag(BufferUsage.Vertex))
        {
            uint stride = (uint)d3dBuffer.StrideBytes;
            uint offset = 0;
            ID3D11Buffer* bufferHandle = d3dBuffer.Handle;
            _context.DeviceContext.Get().IASetVertexBuffers((uint)slot, 1, &bufferHandle, &stride, &offset);
        }
    }

    /// <inheritdoc/>
    public void BindTexture(int slot, IGpuTexture texture)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(texture);

        D3D11Texture d3dTexture = texture as D3D11Texture
            ?? throw new InvalidOperationException($"Expected {nameof(D3D11Texture)}.");
        ID3D11ShaderResourceView* shaderResourceView = d3dTexture.ShaderResourceView;
        ID3D11SamplerState* samplerState = d3dTexture.SamplerState;
        if (shaderResourceView is null || samplerState is null)
        {
            return;
        }

        uint d3dSlot = checked((uint)slot);
        _context.DeviceContext.Get().PSSetShaderResources(d3dSlot, 1, &shaderResourceView);
        _context.DeviceContext.Get().PSSetSamplers(d3dSlot, 1, &samplerState);
    }

    /// <inheritdoc/>
    public void SetUniformInt(string name, int value)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _uniformValues[name] = D3D11UniformValue.FromInt(value);
    }

    /// <inheritdoc/>
    public void SetUniformFloat(string name, float value)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _uniformValues[name] = D3D11UniformValue.FromFloat(value);
    }

    /// <inheritdoc/>
    public void SetUniformVector2(string name, Vector2 value)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _uniformValues[name] = D3D11UniformValue.FromVector2(value);
    }

    /// <inheritdoc/>
    public void SetUniformVector3(string name, Vector3 value)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _uniformValues[name] = D3D11UniformValue.FromVector3(value);
    }

    /// <inheritdoc/>
    public void SetUniformVector4(string name, Vector4 value)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _uniformValues[name] = D3D11UniformValue.FromVector4(value);
    }

    /// <inheritdoc/>
    public void SetUniformMatrix4x4(string name, Matrix4x4 value)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _uniformValues[name] = D3D11UniformValue.FromMatrix4x4(value);
    }

    /// <inheritdoc/>
    public void Draw(int vertexCount, int startVertex)
    {
        ThrowIfDisposed();
        ApplyConstantBuffers();
        _context.DeviceContext.Get().Draw((uint)vertexCount, (uint)startVertex);
    }

    /// <inheritdoc/>
    public void DrawIndexed(int indexCount, int startIndex, int baseVertex)
    {
        ThrowIfDisposed();
        ApplyConstantBuffers();
        _context.DeviceContext.Get().DrawIndexed((uint)indexCount, (uint)startIndex, baseVertex);
    }

    /// <inheritdoc/>
    public void Dispatch(int groupCountX, int groupCountY, int groupCountZ)
    {
        ThrowIfDisposed();
        if (_currentPipelineState?.IsCompute != true)
        {
            throw new InvalidOperationException("A D3D11 compute pipeline state must be bound before dispatch.");
        }

        ApplyConstantBuffers();
        _context.DeviceContext.Get().Dispatch((uint)groupCountX, (uint)groupCountY, (uint)groupCountZ);
    }

    /// <inheritdoc/>
    public void MemoryBarrier()
    {
        ThrowIfDisposed();
        ClearComputeBindings();
    }

    /// <inheritdoc/>
    public void PushDebugGroup(string name)
    {
        ThrowIfDisposed();
    }

    /// <inheritdoc/>
    public void PopDebugGroup()
    {
        ThrowIfDisposed();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        for (int slotIndex = 0; slotIndex < _constantBufferSlots.Length; slotIndex++)
        {
            _constantBufferSlots[slotIndex].Buffer.Dispose();
            _constantBufferSlots[slotIndex].StagingData = null;
            _constantBufferSlots[slotIndex] = default;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private ComPtr<ID3D11Buffer> CreateConstantBuffer(int sizeBytes)
    {
        BufferDesc desc = new()
        {
            ByteWidth = D3D11Helpers.AlignTo16(sizeBytes),
            Usage = Usage.Default,
            BindFlags = (uint)BindFlag.ConstantBuffer,
            CPUAccessFlags = 0,
            MiscFlags = 0,
            StructureByteStride = 0,
        };

        ComPtr<ID3D11Buffer> buffer = default;
        D3D11Helpers.ThrowIfFailed(
            _context.Device.Get().CreateBuffer(ref desc, (SubresourceData*)null, ref buffer),
            "ID3D11Device::CreateBuffer(constant)");
        return buffer;
    }

    private void ApplyPipelineState(D3D11PipelineState pipelineState)
    {
        ID3D11DeviceContext* context = _context.DeviceContext.Handle;
        context->CSSetShader(null, (ID3D11ClassInstance**)null, 0);
        context->IASetInputLayout(pipelineState.InputLayout);
        context->IASetPrimitiveTopology(GetPrimitiveTopology(pipelineState.PrimitiveTopology));
        context->VSSetShader(pipelineState.VertexShader, (ID3D11ClassInstance**)null, 0);
        context->PSSetShader(pipelineState.PixelShader, (ID3D11ClassInstance**)null, 0);
        context->RSSetState(pipelineState.GetRasterizerState(_frontFaceWinding));
        context->OMSetBlendState(pipelineState.BlendState, (float*)null, uint.MaxValue);
        context->OMSetDepthStencilState(pipelineState.DepthStencilState, 0);
    }

    private void ApplyComputePipelineState(D3D11PipelineState pipelineState)
    {
        ID3D11DeviceContext* context = _context.DeviceContext.Handle;
        context->CSSetShader(pipelineState.ComputeShader, (ID3D11ClassInstance**)null, 0);
    }

    private void ApplyConstantBuffers()
    {
        if (_currentPipelineState is null)
        {
            throw new InvalidOperationException("A D3D11 pipeline state must be bound before drawing.");
        }

        IReadOnlyList<D3D11ShaderConstantBufferLayout> constantBuffers = _currentPipelineState.ConstantBuffers;
        for (int constantBufferIndex = 0; constantBufferIndex < constantBuffers.Count; constantBufferIndex++)
        {
            D3D11ShaderConstantBufferLayout layout = constantBuffers[constantBufferIndex];
            int slotIndex = GetConstantBufferSlotIndex(layout.Stage, layout.Slot);
            ref D3D11ConstantBufferSlot constantBufferSlot = ref _constantBufferSlots[slotIndex];
            EnsureConstantBuffer(ref constantBufferSlot, layout.SizeBytes);

            byte[] data = constantBufferSlot.StagingData
                ?? throw new InvalidOperationException("D3D11 constant-buffer staging data was not initialized.");
            Array.Clear(data, 0, constantBufferSlot.SizeBytes);
            for (int variableIndex = 0; variableIndex < layout.Variables.Count; variableIndex++)
            {
                WriteUniform(data, layout.Variables[variableIndex]);
            }

            UpdateConstantBuffer(constantBufferSlot.Buffer, data.AsSpan(0, constantBufferSlot.SizeBytes));
            BindConstantBuffer(layout, constantBufferSlot.Buffer.Handle);
        }
    }

    private void ApplyLightUniforms()
    {
        int appliedLightCount = _lightCount;
        if (appliedLightCount == 0)
        {
            _lightDirectionsAndIntensity[0] = new Vector4(TransformDirection(_fallbackLightDirection), _fallbackLightIntensity);
            _lightColors[0] = _fallbackLightColor;
            appliedLightCount = 1;
        }

        SetUniformInt("LightCount", appliedLightCount);
        SetUniformVector4Array("LightDirectionsAndIntensity", _lightDirectionsAndIntensity);
        SetUniformVector3Array("LightColors", _lightColors);
    }

    private void BindConstantBuffer(D3D11ShaderConstantBufferLayout layout, ID3D11Buffer* buffer)
    {
        uint slot = checked((uint)layout.Slot);
        ID3D11DeviceContext* context = _context.DeviceContext.Handle;
        if (layout.Stage.HasFlag(D3D11ShaderStageFlags.Vertex))
        {
            context->VSSetConstantBuffers(slot, 1, &buffer);
        }

        if (layout.Stage.HasFlag(D3D11ShaderStageFlags.Fragment))
        {
            context->PSSetConstantBuffers(slot, 1, &buffer);
        }

        if (layout.Stage.HasFlag(D3D11ShaderStageFlags.Compute))
        {
            context->CSSetConstantBuffers(slot, 1, &buffer);
        }
    }

    private void EnsureConstantBuffer(ref D3D11ConstantBufferSlot slot, int sizeBytes)
    {
        int alignedSize = checked((int)D3D11Helpers.AlignTo16(Math.Max(sizeBytes, 16)));
        if (slot.Buffer.Handle is not null && slot.SizeBytes >= alignedSize)
        {
            slot.StagingData ??= new byte[slot.SizeBytes];
            return;
        }

        slot.Buffer.Dispose();
        slot.Buffer = CreateConstantBuffer(alignedSize);
        slot.SizeBytes = alignedSize;
        slot.StagingData = new byte[alignedSize];
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

    private static int GetConstantBufferSlotIndex(D3D11ShaderStageFlags stage, int slot)
    {
        if (slot < 0 || slot >= MaxConstantBufferSlots)
        {
            throw new InvalidOperationException($"D3D11 constant buffer slot {slot} is outside the supported range.");
        }

        int stageOffset = stage switch
        {
            D3D11ShaderStageFlags.Vertex => 0,
            D3D11ShaderStageFlags.Fragment => MaxConstantBufferSlots,
            D3D11ShaderStageFlags.Compute => MaxConstantBufferSlots * 2,
            _ => throw new InvalidOperationException($"Unsupported D3D11 shader stage flags '{stage}'."),
        };

        return stageOffset + slot;
    }

    private void SetUniformVector3Array(string name, Vector3[] values)
    {
        _uniformValues[name] = D3D11UniformValue.FromVector3Array(values);
    }

    private void SetUniformVector4Array(string name, Vector4[] values)
    {
        _uniformValues[name] = D3D11UniformValue.FromVector4Array(values);
    }

    private void UpdateConstantBuffer(ComPtr<ID3D11Buffer> buffer, ReadOnlySpan<byte> data)
    {
        fixed (byte* dataPointer = data)
        {
            _context.DeviceContext.Get().UpdateSubresource((ID3D11Resource*)buffer.Handle, 0, (Box*)null, dataPointer, 0, 0);
        }
    }

    private void WriteUniform(Span<byte> data, D3D11ShaderVariableLayout variable)
    {
        if (!_uniformValues.TryGetValue(variable.Name, out D3D11UniformValue value))
        {
            return;
        }

        if (variable.IsArray)
        {
            WriteUniformArray(data, variable, value);
            return;
        }

        switch (variable.Kind)
        {
            case D3D11ShaderVariableKind.Int when value.Kind == D3D11UniformValueKind.Int:
                WriteValue(data, variable.OffsetBytes, value.IntValue);
                break;
            case D3D11ShaderVariableKind.Float when variable.ComponentCount == 1 && value.Kind == D3D11UniformValueKind.Float:
                WriteValue(data, variable.OffsetBytes, value.FloatValue);
                break;
            case D3D11ShaderVariableKind.Float when variable.ComponentCount == 2 && value.Kind == D3D11UniformValueKind.Vector2:
                WriteValue(data, variable.OffsetBytes, value.Vector2Value);
                break;
            case D3D11ShaderVariableKind.Float when variable.ComponentCount == 3 && value.Kind == D3D11UniformValueKind.Vector3:
                WriteValue(data, variable.OffsetBytes, value.Vector3Value);
                break;
            case D3D11ShaderVariableKind.Float when variable.ComponentCount == 4 && value.Kind == D3D11UniformValueKind.Vector4:
                WriteValue(data, variable.OffsetBytes, value.Vector4Value);
                break;
            case D3D11ShaderVariableKind.Matrix4x4 when value.Kind == D3D11UniformValueKind.Matrix4x4:
                WriteValue(data, variable.OffsetBytes, value.MatrixValue);
                break;
        }
    }

    private static void WriteUniformArray(Span<byte> data, D3D11ShaderVariableLayout variable, D3D11UniformValue value)
    {
        if (variable.Kind != D3D11ShaderVariableKind.Float)
        {
            return;
        }

        if (variable.ComponentCount == 3 && value.Kind == D3D11UniformValueKind.Vector3Array && value.Vector3ArrayValue is not null)
        {
            int count = Math.Min(variable.ArrayLength, value.Vector3ArrayValue.Length);
            for (int elementIndex = 0; elementIndex < count; elementIndex++)
            {
                WriteValue(data, variable.OffsetBytes + (elementIndex * variable.ArrayStrideBytes), value.Vector3ArrayValue[elementIndex]);
            }

            return;
        }

        if (variable.ComponentCount == 4 && value.Kind == D3D11UniformValueKind.Vector4Array && value.Vector4ArrayValue is not null)
        {
            int count = Math.Min(variable.ArrayLength, value.Vector4ArrayValue.Length);
            for (int elementIndex = 0; elementIndex < count; elementIndex++)
            {
                WriteValue(data, variable.OffsetBytes + (elementIndex * variable.ArrayStrideBytes), value.Vector4ArrayValue[elementIndex]);
            }
        }
    }

    private static void WriteValue<TValue>(Span<byte> data, int offsetBytes, TValue value) where TValue : unmanaged
    {
        MemoryMarshal.Write(data[offsetBytes..], in value);
    }

    private void BindComputeBuffer(int slot, D3D11Buffer buffer)
    {
        if (slot >= ComputeFirstOutputBufferSlot)
        {
            ID3D11UnorderedAccessView* unorderedAccessView = buffer.UnorderedAccessView;
            if (unorderedAccessView is null)
            {
                throw new InvalidOperationException("The D3D11 compute output buffer does not have an unordered-access view.");
            }

            uint initialCount = uint.MaxValue;
            uint unorderedAccessSlot = checked((uint)(slot - ComputeFirstOutputBufferSlot));
            _context.DeviceContext.Get().CSSetUnorderedAccessViews(unorderedAccessSlot, 1, &unorderedAccessView, &initialCount);
            return;
        }

        ID3D11ShaderResourceView* shaderResourceView = buffer.ShaderResourceView;
        if (shaderResourceView is null)
        {
            throw new InvalidOperationException("The D3D11 compute input buffer does not have a shader-resource view.");
        }

        _context.DeviceContext.Get().CSSetShaderResources((uint)slot, 1, &shaderResourceView);
    }

    private void ClearComputeBindings()
    {
        ID3D11DeviceContext* context = _context.DeviceContext.Handle;
        ID3D11ShaderResourceView* nullShaderResourceView = null;
        for (uint slot = 0; slot < ComputeShaderResourceCount; slot++)
        {
            context->CSSetShaderResources(slot, 1, &nullShaderResourceView);
        }

        ID3D11UnorderedAccessView* nullUnorderedAccessView = null;
        uint initialCount = 0;
        for (uint slot = 0; slot < ComputeOutputBufferCount; slot++)
        {
            context->CSSetUnorderedAccessViews(slot, 1, &nullUnorderedAccessView, &initialCount);
        }

        ID3D11Buffer* nullConstantBuffer = null;
        context->CSSetConstantBuffers(0, 1, &nullConstantBuffer);
        context->CSSetShader(null, (ID3D11ClassInstance**)null, 0);
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

    private static D3DPrimitiveTopology GetPrimitiveTopology(PrimitiveTopology primitiveTopology)
    {
        return primitiveTopology switch
        {
            PrimitiveTopology.Points => D3DPrimitiveTopology.D3DPrimitiveTopologyPointlist,
            PrimitiveTopology.Lines => D3DPrimitiveTopology.D3DPrimitiveTopologyLinelist,
            PrimitiveTopology.LineStrip => D3DPrimitiveTopology.D3DPrimitiveTopologyLinestrip,
            PrimitiveTopology.TriangleStrip => D3DPrimitiveTopology.D3DPrimitiveTopologyTrianglestrip,
            _ => D3DPrimitiveTopology.D3DPrimitiveTopologyTrianglelist,
        };
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}