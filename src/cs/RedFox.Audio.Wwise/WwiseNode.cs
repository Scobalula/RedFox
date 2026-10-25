namespace RedFox.Audio.Wwise;

public sealed record WwiseNode(uint Id, WwiseObjectType Type, uint ParentId, uint[] SourceIds);
