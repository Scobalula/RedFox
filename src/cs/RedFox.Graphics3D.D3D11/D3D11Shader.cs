using RedFox.Graphics3D.Rendering;
using System;

namespace RedFox.Graphics3D.D3D11;

/// <summary>
/// Represents compiled Direct3D 11 shader bytecode.
/// </summary>
public sealed class D3D11Shader : IGpuShader
{
    private byte[] _bytecode;

    internal D3D11Shader(byte[] bytecode, ShaderStage stage, D3D11ShaderReflectionResult reflection)
    {
        _bytecode = bytecode ?? throw new ArgumentNullException(nameof(bytecode));
        ArgumentNullException.ThrowIfNull(reflection);
        ConstantBuffers = reflection.ConstantBuffers;
        ResourceBindings = reflection.ResourceBindings;
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

    internal IReadOnlyList<D3D11ShaderResourceBinding> ResourceBindings { get; }

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