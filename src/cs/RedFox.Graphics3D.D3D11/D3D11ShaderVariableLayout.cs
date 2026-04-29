namespace RedFox.Graphics3D.D3D11;

internal sealed class D3D11ShaderVariableLayout
{
    public D3D11ShaderVariableLayout(
        string name,
        D3D11ShaderVariableKind kind,
        int offsetBytes,
        int componentCount,
        int sizeBytes,
        bool isArray,
        int arrayLength,
        int arrayStrideBytes)
    {
        Name = name;
        Kind = kind;
        OffsetBytes = offsetBytes;
        ComponentCount = componentCount;
        SizeBytes = sizeBytes;
        IsArray = isArray;
        ArrayLength = arrayLength;
        ArrayStrideBytes = arrayStrideBytes;
    }

    public string Name { get; }

    public D3D11ShaderVariableKind Kind { get; }

    public int OffsetBytes { get; }

    public int ComponentCount { get; }

    public int SizeBytes { get; }

    public bool IsArray { get; }

    public int ArrayLength { get; }

    public int ArrayStrideBytes { get; }
}