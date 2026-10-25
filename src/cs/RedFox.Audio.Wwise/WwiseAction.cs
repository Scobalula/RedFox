namespace RedFox.Audio.Wwise;

public sealed record WwiseAction(uint Id, ushort Type, uint TargetId)
{
    private const ushort PlayType = 0x0403;

    public bool IsPlay => Type == PlayType;
}
