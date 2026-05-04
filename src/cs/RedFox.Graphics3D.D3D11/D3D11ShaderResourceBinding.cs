namespace RedFox.Graphics3D.D3D11;

internal sealed class D3D11ShaderResourceBinding
{
    public D3D11ShaderResourceBinding(string name, int slot, D3D11ShaderStageFlags stage)
    {
        Name = name;
        Slot = slot;
        Stage = stage;
    }

    public string Name { get; }

    public int Slot { get; }

    public D3D11ShaderStageFlags Stage { get; }
}
