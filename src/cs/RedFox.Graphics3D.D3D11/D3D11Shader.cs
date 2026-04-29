using RedFox.Graphics3D.Rendering;
using System;

namespace RedFox.Graphics3D.D3D11;

/// <summary>
/// Represents compiled Direct3D 11 shader bytecode.
/// </summary>
public sealed class D3D11Shader : IGpuShader
{
    private byte[] _bytecode;

    internal D3D11Shader(byte[] bytecode, ShaderStage stage, IReadOnlyList<D3D11ShaderConstantBufferLayout> constantBuffers)
    {
        _bytecode = bytecode ?? throw new ArgumentNullException(nameof(bytecode));
        ConstantBuffers = constantBuffers ?? throw new ArgumentNullException(nameof(constantBuffers));
        Stage = stage;
    }

    internal ReadOnlySpan<byte> Bytecode
    {
        get
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            return _bytecode;
        }
    }

    internal ShaderStage Stage { get; }

    internal IReadOnlyList<D3D11ShaderConstantBufferLayout> ConstantBuffers { get; }

    /// <inheritdoc/>
    public bool IsDisposed { get; private set; }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        _bytecode = [];
        IsDisposed = true;
        GC.SuppressFinalize(this);
    }
}