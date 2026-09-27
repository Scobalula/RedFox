namespace RedFox.Graphics3D.D3D11;

internal sealed class D3D11ShaderResourceBinding(string name, int slot, D3D11ShaderStageFlags stage)
{
    public string Name { get; } = name;

    public int Slot { get; } = slot;

    public D3D11ShaderStageFlags Stage { get; } = stage;
}
