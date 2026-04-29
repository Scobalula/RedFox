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
        ConstantBuffers = constantBuffers ?? throw new ArgumentNullException(nameof(constantBuffers));
        PrimitiveTopology = primitiveTopology;
    }

    internal D3D11PipelineState(ComPtr<ID3D11ComputeShader> computeShader, IReadOnlyList<D3D11ShaderConstantBufferLayout> constantBuffers)
    {
        _computeShader = computeShader;
        _vertexAttributes = [];
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
}