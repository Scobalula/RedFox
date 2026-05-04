using System;

namespace RedFox.Graphics3D.D3D11;

internal sealed class D3D11ShaderReflectionResult
{
    public D3D11ShaderReflectionResult(
        IReadOnlyList<D3D11ShaderConstantBufferLayout> constantBuffers,
        IReadOnlyList<D3D11ShaderResourceBinding> resourceBindings)
    {
        ConstantBuffers = constantBuffers ?? throw new ArgumentNullException(nameof(constantBuffers));
        ResourceBindings = resourceBindings ?? throw new ArgumentNullException(nameof(resourceBindings));
    }

    public IReadOnlyList<D3D11ShaderConstantBufferLayout> ConstantBuffers { get; }

    public IReadOnlyList<D3D11ShaderResourceBinding> ResourceBindings { get; }
}
