using System.Collections.Generic;

namespace RedFox.Graphics3D.D3D11;

internal sealed class D3D11ShaderConstantBufferLayout(string name, int slot, D3D11ShaderStageFlags stage, int sizeBytes, IReadOnlyList<D3D11ShaderVariableLayout> variables)
{
    public string Name { get; } = name;

    public int Slot { get; } = slot;

    public D3D11ShaderStageFlags Stage { get; } = stage;

    public int SizeBytes { get; } = sizeBytes;

    public IReadOnlyList<D3D11ShaderVariableLayout> Variables { get; } = variables;
}
