namespace RedFox.Graphics3D.D3D11;

internal sealed class D3D11ShaderVariableLayout(string name, D3D11ShaderVariableKind kind, int offsetBytes, int componentCount, int sizeBytes, bool isArray, int arrayLength, int arrayStrideBytes)
{
    public string Name { get; } = name;

    public D3D11ShaderVariableKind Kind { get; } = kind;

    public int OffsetBytes { get; } = offsetBytes;

    public int ComponentCount { get; } = componentCount;

    public int SizeBytes { get; } = sizeBytes;

    public bool IsArray { get; } = isArray;

    public int ArrayLength { get; } = arrayLength;

    public int ArrayStrideBytes { get; } = arrayStrideBytes;
}
