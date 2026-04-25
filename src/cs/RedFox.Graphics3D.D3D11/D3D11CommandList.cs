using System.Numerics;
using System.Runtime.InteropServices;
using RedFox.Graphics3D.Rendering.Backend;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;

namespace RedFox.Graphics3D.D3D11;

/// <summary>
/// Represents the Direct3D 11 immediate command list implementation.
/// </summary>
public sealed unsafe class D3D11CommandList : ICommandList, IDisposable
{
    private const int ComputeFirstOutputBufferSlot = 5;
    private const int ComputeOutputBufferCount = 2;
    private const int ComputeShaderResourceCount = 5;
    private const int MaxLights = 4;

    private ComPtr<ID3D11Buffer> _frameConstantsBuffer;
    private ComPtr<ID3D11Buffer> _lightingConstantsBuffer;
    private ComPtr<ID3D11Buffer> _skinningConstantsBuffer;
    private readonly D3D11Context _context;
    private readonly Vector3[] _lightColors = new Vector3[MaxLights];
    private readonly Vector4[] _lightDirectionsAndIntensity = new Vector4[MaxLights];
    private D3D11PipelineState? _currentPipelineState;
    private ID3D11DepthStencilView* _currentDepthStencilView;
    private ID3D11RenderTargetView* _currentRenderTargetView;
    private Vector3 _ambientColor;
    private bool _disposed;
    private float _fadeEndDistance;
    private float _fadeStartDistance;
    private Vector4 _baseColor = Vector4.One;
    private Vector3 _cameraPosition;
    private Vector3 _fallbackLightColor = Vector3.One;
    private Vector3 _fallbackLightDirection = -Vector3.UnitY;
    private float _fallbackLightIntensity = 1.0f;
    private FaceWinding _frontFaceWinding = FaceWinding.CounterClockwise;
    private int _lightCount;
    private float _lineHalfWidthPx = 0.5f;
    private Matrix4x4 _model = Matrix4x4.Identity;
    private Matrix4x4 _projection = Matrix4x4.Identity;
    private Matrix4x4 _sceneAxis = Matrix4x4.Identity;
    private SkinningMode _skinningMode = SkinningMode.Linear;
    private int _skinInfluenceCount;
    private float _materialSpecularPower = 32.0f;
    private float _materialSpecularStrength = 0.28f;
    private bool _useViewBasedLighting;
    private Matrix4x4 _view = Matrix4x4.Identity;
    private int _vertexCount;
    private Vector2 _viewportSize = Vector2.One;

    /// <summary>
    /// Initializes a new instance of the <see cref="D3D11CommandList"/> class.
    /// </summary>
    /// <param name="context">The owning Direct3D 11 context wrapper.</param>
    internal D3D11CommandList(D3D11Context context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _frameConstantsBuffer = CreateConstantBuffer(Math.Max(Marshal.SizeOf<D3D11FrameConstants>(), Marshal.SizeOf<D3D11LineConstants>()));
        _lightingConstantsBuffer = CreateConstantBuffer(Math.Max(Marshal.SizeOf<D3D11LightingConstants>(), Marshal.SizeOf<D3D11FadeConstants>()));
        _skinningConstantsBuffer = CreateConstantBuffer(Marshal.SizeOf<D3D11SkinningConstants>());
        _currentRenderTargetView = context.DefaultRenderTargetView;
        _currentDepthStencilView = context.DefaultDepthStencilView;
    }

    /// <inheritdoc/>
    public void Reset()
    {
        ThrowIfDisposed();
        _currentPipelineState = null;
        _ambientColor = Vector3.Zero;
        _baseColor = Vector4.One;
        _cameraPosition = Vector3.Zero;
        _fadeEndDistance = 0.0f;
        _fadeStartDistance = 0.0f;
        _fallbackLightDirection = -Vector3.UnitY;
        _fallbackLightColor = Vector3.One;
        _fallbackLightIntensity = 1.0f;
        _frontFaceWinding = FaceWinding.CounterClockwise;
        _lightCount = 0;
        _lineHalfWidthPx = 0.5f;
        _model = Matrix4x4.Identity;
        _projection = Matrix4x4.Identity;
        _sceneAxis = Matrix4x4.Identity;
        _skinningMode = SkinningMode.Linear;
        _skinInfluenceCount = 0;
        _materialSpecularPower = 32.0f;
        _materialSpecularStrength = 0.28f;
        _useViewBasedLighting = false;
        _view = Matrix4x4.Identity;
        _vertexCount = 0;
        _viewportSize = Vector2.One;
        SetRenderTarget(null);
    }

    /// <inheritdoc/>
    public void SetViewport(int width, int height)
    {
        ThrowIfDisposed();
        int safeWidth = Math.Max(1, width);
        int safeHeight = Math.Max(1, height);
        _viewportSize = new Vector2(safeWidth, safeHeight);
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
    public void SetPipelineState(IGpuPipelineState pipelineState)
    {
        ThrowIfDisposed();
        _currentPipelineState = pipelineState as D3D11PipelineState
            ?? throw new InvalidOperationException($"Expected {nameof(D3D11PipelineState)}.");
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
        _ambientColor = ambientColor;
    }

    /// <inheritdoc/>
    public void SetUseViewBasedLighting(bool enabled)
    {
        ThrowIfDisposed();
        _useViewBasedLighting = enabled;
    }

    /// <inheritdoc/>
    public void SetSkinningMode(SkinningMode skinningMode)
    {
        ThrowIfDisposed();
        _skinningMode = skinningMode;
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
    }

    /// <inheritdoc/>
    public void SetUniformInt(ReadOnlySpan<char> name, int value)
    {
        ThrowIfDisposed();
        string uniformName = name.ToString();
        if (uniformName.Equals("SkinningMode", StringComparison.Ordinal))
        {
            _skinningMode = (SkinningMode)value;
            return;
        }

        if (uniformName.Equals("VertexCount", StringComparison.Ordinal))
        {
            _vertexCount = value;
            return;
        }

        if (uniformName.Equals("SkinInfluenceCount", StringComparison.Ordinal))
        {
            _skinInfluenceCount = value;
        }
    }

    /// <inheritdoc/>
    public void SetUniformFloat(ReadOnlySpan<char> name, float value)
    {
        ThrowIfDisposed();
        switch (name.ToString())
        {
            case "LineHalfWidthPx":
                _lineHalfWidthPx = value;
                break;
            case "FadeStartDistance":
                _fadeStartDistance = value;
                break;
            case "FadeEndDistance":
                _fadeEndDistance = value;
                break;
            case "MaterialSpecularStrength":
                _materialSpecularStrength = value;
                break;
            case "MaterialSpecularPower":
                _materialSpecularPower = value;
                break;
        }
    }

    /// <inheritdoc/>
    public void SetUniformVector2(ReadOnlySpan<char> name, Vector2 value)
    {
        ThrowIfDisposed();
        if (name.ToString().Equals("ViewportSize", StringComparison.Ordinal))
        {
            _viewportSize = value;
        }
    }

    /// <inheritdoc/>
    public void SetUniformVector3(ReadOnlySpan<char> name, Vector3 value)
    {
        ThrowIfDisposed();
        if (name.ToString().Equals("CameraPosition", StringComparison.Ordinal))
        {
            _cameraPosition = value;
        }
    }

    /// <inheritdoc/>
    public void SetUniformVector4(ReadOnlySpan<char> name, Vector4 value)
    {
        ThrowIfDisposed();
        if (name.ToString().Equals("BaseColor", StringComparison.Ordinal))
        {
            _baseColor = value;
        }
    }

    /// <inheritdoc/>
    public void SetUniformMatrix4x4(ReadOnlySpan<char> name, Matrix4x4 value)
    {
        ThrowIfDisposed();
        switch (name.ToString())
        {
            case "Model":
                _model = value;
                break;
            case "SceneAxis":
                _sceneAxis = value;
                break;
            case "View":
                _view = value;
                break;
            case "Projection":
                _projection = value;
                break;
        }
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

        ApplySkinningConstants();
        _context.DeviceContext.Get().Dispatch((uint)groupCountX, (uint)groupCountY, (uint)groupCountZ);
    }

    /// <inheritdoc/>
    public void MemoryBarrier()
    {
        ThrowIfDisposed();
        ClearComputeBindings();
    }

    /// <inheritdoc/>
    public void PushDebugGroup(ReadOnlySpan<char> name)
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

        _lightingConstantsBuffer.Dispose();
        _frameConstantsBuffer.Dispose();
        _skinningConstantsBuffer.Dispose();
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

        if (_currentPipelineState.UsesLineConstants)
        {
            D3D11LineConstants lineConstants = new()
            {
                Model = _model,
                SceneAxis = _sceneAxis,
                View = _view,
                Projection = _projection,
                ViewportAndHalfWidth = new Vector4(_viewportSize.X, _viewportSize.Y, _lineHalfWidthPx, 0.0f),
            };
            D3D11FadeConstants fadeConstants = new()
            {
                CameraAndFadeStart = new Vector4(_cameraPosition, _fadeStartDistance),
                FadeEnd = new Vector4(_fadeEndDistance, 0.0f, 0.0f, 0.0f),
            };
            UpdateConstantBuffer(_frameConstantsBuffer, lineConstants);
            UpdateConstantBuffer(_lightingConstantsBuffer, fadeConstants);
        }
        else
        {
            D3D11FrameConstants frameConstants = new()
            {
                Model = _model,
                SceneAxis = _sceneAxis,
                View = _view,
                Projection = _projection,
            };
            D3D11LightingConstants lightingConstants = BuildLightingConstants();
            UpdateConstantBuffer(_frameConstantsBuffer, frameConstants);
            UpdateConstantBuffer(_lightingConstantsBuffer, lightingConstants);
        }

        ID3D11Buffer* frameBuffer = _frameConstantsBuffer.Handle;
        ID3D11Buffer* lightingBuffer = _lightingConstantsBuffer.Handle;
        ID3D11DeviceContext* context = _context.DeviceContext.Handle;
        context->VSSetConstantBuffers(0, 1, &frameBuffer);
        context->VSSetConstantBuffers(1, 1, &lightingBuffer);
        context->PSSetConstantBuffers(0, 1, &frameBuffer);
        context->PSSetConstantBuffers(1, 1, &lightingBuffer);
    }

    private D3D11LightingConstants BuildLightingConstants()
    {
        int appliedLightCount = _lightCount;
        Vector4[] directions = new Vector4[MaxLights];
        Vector4[] colors = new Vector4[MaxLights];
        if (appliedLightCount == 0)
        {
            directions[0] = new Vector4(TransformDirection(_fallbackLightDirection), _fallbackLightIntensity);
            colors[0] = new Vector4(_fallbackLightColor, 0.0f);
            appliedLightCount = 1;
        }
        else
        {
            for (int lightIndex = 0; lightIndex < MaxLights; lightIndex++)
            {
                directions[lightIndex] = _lightDirectionsAndIntensity[lightIndex];
                colors[lightIndex] = new Vector4(_lightColors[lightIndex], 0.0f);
            }
        }

        return new D3D11LightingConstants
        {
            AmbientColor = _ambientColor,
            LightCount = appliedLightCount,
            LightDirectionAndIntensity0 = directions[0],
            LightDirectionAndIntensity1 = directions[1],
            LightDirectionAndIntensity2 = directions[2],
            LightDirectionAndIntensity3 = directions[3],
            LightColor0 = colors[0],
            LightColor1 = colors[1],
            LightColor2 = colors[2],
            LightColor3 = colors[3],
            CameraPosition = _cameraPosition,
            UseViewBasedLighting = _useViewBasedLighting ? 1 : 0,
            BaseColor = _baseColor,
            Specular = new Vector4(_materialSpecularStrength, _materialSpecularPower, 0.0f, 0.0f),
        };
    }

    private void UpdateConstantBuffer<TValue>(ComPtr<ID3D11Buffer> buffer, TValue value) where TValue : unmanaged
    {
        _context.DeviceContext.Get().UpdateSubresource((ID3D11Resource*)buffer.Handle, 0, (Box*)null, &value, 0, 0);
    }

    private void ApplySkinningConstants()
    {
        D3D11SkinningConstants constants = new()
        {
            VertexCount = _vertexCount,
            SkinInfluenceCount = _skinInfluenceCount,
            SkinningMode = (int)_skinningMode,
            Padding = 0,
        };

        UpdateConstantBuffer(_skinningConstantsBuffer, constants);
        ID3D11Buffer* skinningConstantsBuffer = _skinningConstantsBuffer.Handle;
        _context.DeviceContext.Get().CSSetConstantBuffers(0, 1, &skinningConstantsBuffer);
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