using RedFox.Graphics3D.Rendering;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using System;

namespace RedFox.Graphics3D.D3D11;

/// <summary>
/// Represents a Direct3D 11 graphics pipeline-state resource.
/// </summary>
public sealed unsafe class D3D11PipelineState : IGpuPipelineState
{
    private readonly VertexAttribute[] _vertexAttributes;
    private readonly D3D11ShaderResourceBinding[] _resourceBindings;
    private ComPtr<ID3D11BlendState> _blendState;
    private ComPtr<ID3D11DepthStencilState> _depthStencilState;
    private ComPtr<ID3D11InputLayout> _inputLayout;
    private ComPtr<ID3D11PixelShader> _pixelShader;
    private ComPtr<ID3D11ComputeShader> _computeShader;
    private ComPtr<ID3D11RasterizerState> _clockwiseRasterizerState;
    private ComPtr<ID3D11RasterizerState> _counterClockwiseRasterizerState;
    private ComPtr<ID3D11VertexShader> _vertexShader;

    internal D3D11PipelineState(
        ComPtr<ID3D11VertexShader> vertexShader,
        ComPtr<ID3D11PixelShader> pixelShader,
        ComPtr<ID3D11InputLayout> inputLayout,
        ComPtr<ID3D11RasterizerState> counterClockwiseRasterizerState,
        ComPtr<ID3D11RasterizerState> clockwiseRasterizerState,
        ComPtr<ID3D11BlendState> blendState,
        ComPtr<ID3D11DepthStencilState> depthStencilState,
        ReadOnlySpan<VertexAttribute> vertexAttributes,
        IReadOnlyList<D3D11ShaderConstantBufferLayout> constantBuffers,
        IReadOnlyList<D3D11ShaderResourceBinding> resourceBindings,
        PrimitiveTopology primitiveTopology)
    {
        _vertexShader = vertexShader;
        _pixelShader = pixelShader;
        _inputLayout = inputLayout;
        _counterClockwiseRasterizerState = counterClockwiseRasterizerState;
        _clockwiseRasterizerState = clockwiseRasterizerState;
        _blendState = blendState;
        _depthStencilState = depthStencilState;
        _vertexAttributes = vertexAttributes.ToArray();
        _resourceBindings = CopyResourceBindings(resourceBindings);
        ConstantBuffers = constantBuffers ?? throw new ArgumentNullException(nameof(constantBuffers));
        PrimitiveTopology = primitiveTopology;
    }

    internal D3D11PipelineState(
        ComPtr<ID3D11ComputeShader> computeShader,
        IReadOnlyList<D3D11ShaderConstantBufferLayout> constantBuffers,
        IReadOnlyList<D3D11ShaderResourceBinding> resourceBindings)
    {
        _computeShader = computeShader;
        _vertexAttributes = [];
        _resourceBindings = CopyResourceBindings(resourceBindings);
        ConstantBuffers = constantBuffers ?? throw new ArgumentNullException(nameof(constantBuffers));
        PrimitiveTopology = PrimitiveTopology.Triangles;
    }

    internal ID3D11VertexShader* VertexShader => _vertexShader.Handle;

    internal ID3D11PixelShader* PixelShader => _pixelShader.Handle;

    internal ID3D11InputLayout* InputLayout => _inputLayout.Handle;

    internal ID3D11ComputeShader* ComputeShader => _computeShader.Handle;

    internal ID3D11BlendState* BlendState => _blendState.Handle;

    internal ID3D11DepthStencilState* DepthStencilState => _depthStencilState.Handle;

    internal PrimitiveTopology PrimitiveTopology { get; }

    internal ReadOnlySpan<VertexAttribute> VertexAttributes => _vertexAttributes;

    internal IReadOnlyList<D3D11ShaderConstantBufferLayout> ConstantBuffers { get; }

    internal bool IsCompute => _computeShader.Handle is not null;

    internal ID3D11RasterizerState* GetRasterizerState(FaceWinding frontFaceWinding)
    {
        return frontFaceWinding == FaceWinding.CounterClockwise
            ? _counterClockwiseRasterizerState.Handle
            : _clockwiseRasterizerState.Handle;
    }

    /// <inheritdoc/>
    public bool IsDisposed { get; private set; }

    /// <inheritdoc/>
    public bool TryGetBufferSlot(string name, out int slot)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            slot = -1;
            return false;
        }

        for (int attributeIndex = 0; attributeIndex < _vertexAttributes.Length; attributeIndex++)
        {
            if (_vertexAttributes[attributeIndex].Name.Equals(name, StringComparison.Ordinal))
            {
                slot = attributeIndex;
                return true;
            }
        }

        for (int resourceIndex = 0; resourceIndex < _resourceBindings.Length; resourceIndex++)
        {
            D3D11ShaderResourceBinding binding = _resourceBindings[resourceIndex];
            if (binding.Name.Equals(name, StringComparison.Ordinal))
            {
                slot = binding.Slot;
                return true;
            }
        }

        slot = -1;
        return false;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        _depthStencilState.Dispose();
        _blendState.Dispose();
        _clockwiseRasterizerState.Dispose();
        _counterClockwiseRasterizerState.Dispose();
        _inputLayout.Dispose();
        _pixelShader.Dispose();
        _computeShader.Dispose();
        _vertexShader.Dispose();
        IsDisposed = true;
        GC.SuppressFinalize(this);
    }

    private static D3D11ShaderResourceBinding[] CopyResourceBindings(IReadOnlyList<D3D11ShaderResourceBinding> resourceBindings)
    {
        ArgumentNullException.ThrowIfNull(resourceBindings);

        D3D11ShaderResourceBinding[] copy = new D3D11ShaderResourceBinding[resourceBindings.Count];
        for (int index = 0; index < resourceBindings.Count; index++)
        {
            copy[index] = resourceBindings[index];
        }

        return copy;
    }
}
