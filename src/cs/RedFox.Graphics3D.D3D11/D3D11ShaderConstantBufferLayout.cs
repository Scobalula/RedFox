using System.Collections.Generic;

namespace RedFox.Graphics3D.D3D11;

internal sealed class D3D11ShaderConstantBufferLayout
{
    public D3D11ShaderConstantBufferLayout(
        string name,
        int slot,
        D3D11ShaderStageFlags stage,
        int sizeBytes,
        IReadOnlyList<D3D11ShaderVariableLayout> variables)
    {
        Name = name;
        Slot = slot;
        Stage = stage;
        SizeBytes = sizeBytes;
        Variables = variables;
    }

    public string Name { get; }

    public int Slot { get; }

    public D3D11ShaderStageFlags Stage { get; }

    public int SizeBytes { get; }

    public IReadOnlyList<D3D11ShaderVariableLayout> Variables { get; }
}