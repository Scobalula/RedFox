using System;

namespace RedFox.Graphics3D.D3D11;

internal sealed class D3D11ShaderReflectionResult(IReadOnlyList<D3D11ShaderConstantBufferLayout> constantBuffers, IReadOnlyList<D3D11ShaderResourceBinding> resourceBindings)
{
    public IReadOnlyList<D3D11ShaderConstantBufferLayout> ConstantBuffers { get; } = constantBuffers ?? throw new ArgumentNullException(nameof(constantBuffers));

    public IReadOnlyList<D3D11ShaderResourceBinding> ResourceBindings { get; } = resourceBindings ?? throw new ArgumentNullException(nameof(resourceBindings));
}
