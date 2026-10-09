namespace RedFox.Compression.ZStandard;

internal unsafe struct ZStandardBuffer
{
    internal void*	Data;
    internal nuint	Size;
    internal nuint	Position;
}
