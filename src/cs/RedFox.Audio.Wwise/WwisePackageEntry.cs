namespace RedFox.Audio.Wwise;

public sealed record WwisePackageEntry(ulong Id, WwisePackageEntryType Type, uint LanguageId, long Offset, uint Size);
