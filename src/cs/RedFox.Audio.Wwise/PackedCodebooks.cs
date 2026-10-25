namespace RedFox.Audio.Wwise;

internal sealed class PackedCodebooks
{
    private const string ResourceName = "RedFox.Audio.Wwise.packed_codebooks.bin";

    private static readonly Lazy<PackedCodebooks> _default = new(Load);

    private readonly int[] _offsets;

    private readonly byte[] _data;

    public static PackedCodebooks Default => _default.Value;

    private PackedCodebooks(int[] offsets, byte[] data)
    {
        _offsets = offsets;
        _data = data;
    }

    public ReadOnlySpan<byte> GetCodebook(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _offsets.Length);

        var offset = _offsets[index];

        if (offset < 0)
            throw new InvalidDataException($"The codebook library holds no codebook for index {index}.");

        var end = _data.Length;

        for (var next = index + 1; next < _offsets.Length; next++)
        {
            if (_offsets[next] >= 0)
            {
                end = _offsets[next];

                break;
            }
        }

        return _data.AsSpan(offset, end - offset);
    }

    private static PackedCodebooks Load()
    {
        using var stream = typeof(PackedCodebooks).Assembly.GetManifestResourceStream(ResourceName) ?? throw new InvalidOperationException($"The embedded resource {ResourceName} is missing.");
        using var reader = new BinaryReader(stream);

        var entryCount = reader.ReadInt32();
        var dataSize = reader.ReadInt32();
        var offsets = new int[entryCount];

        for (var i = 0; i < entryCount; i++)
            offsets[i] = reader.ReadInt32();

        return new PackedCodebooks(offsets, reader.ReadBytes(dataSize));
    }
}
