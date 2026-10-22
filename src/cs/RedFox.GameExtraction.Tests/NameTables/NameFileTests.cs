using System.Text;
using RedFox.GameExtraction.Hashing;

namespace RedFox.GameExtraction.Tests.NameTables;

[TestClass]
public sealed class NameFileTests
{
    [TestMethod]
    [DataRow(NameFileFlags.None)]
    [DataRow(NameFileFlags.Compressed)]
    [DataRow(NameFileFlags.Checksum)]
    [DataRow(NameFileFlags.Compressed | NameFileFlags.Checksum)]
    public void RoundTrip_PreservesMappingsMetadataMagicAndStreamPosition(NameFileFlags flags)
    {
        var source = new NameTable("fnv");
        source.Metadata["Commit"] = "example";
        source.Add(new byte[] { 0, 1 }, "characters/狐狸/🦊.model");
        source.Add(1UL, "characters/狐狸/🦊.model");
        source.Add(Array.Empty<byte>(), "another");
        source.Add(new byte[256], "long key");
        var original = source.ToArray();
        using var stream = new MemoryStream();
        stream.WriteByte(123);
        NameFile.SaveBinary(stream, source, flags);
        var end = stream.Position;
        stream.WriteByte(231);
        stream.Position = 1;
        using var reader = new BinaryReader(stream, Encoding.UTF8, true);

        Assert.AreEqual(0x454C494648534148UL, reader.ReadUInt64());

        stream.Position = 1;
        var loaded = NameFile.LoadBinary(reader);

        Assert.AreEqual(end, stream.Position);
        Assert.AreEqual(231, stream.ReadByte());
        Assert.AreEqual("fnv", loaded.Name);
        Assert.AreEqual("example", loaded.Metadata["Commit"]);
        CollectionAssert.AreEquivalent(original, loaded.ToArray());
        CollectionAssert.AreEqual(original, source.ToArray());

        loaded.Add(1UL, "replacement");
        loaded.TrimExcess();

        Assert.IsTrue(loaded.TryGetValue(new byte[] { 0, 1 }, out var shared));
        Assert.AreEqual("characters/狐狸/🦊.model", shared);
        Assert.IsTrue(loaded.TryGetValue(1UL, out var replacement));
        Assert.AreEqual("replacement", replacement);
    }

    [TestMethod]
    [DataRow(NameFileFlags.None)]
    [DataRow(NameFileFlags.Compressed | NameFileFlags.Checksum)]
    public void RoundTrip_HandlesEmptyTable(NameFileFlags flags)
    {
        using var stream = new MemoryStream();
        NameFile.SaveBinary(stream, new NameTable("empty"), flags);
        stream.Position = 0;
        var loaded = NameFile.LoadBinary(stream);

        Assert.AreEqual("empty", loaded.Name);
        Assert.AreEqual(0, loaded.Count);
        Assert.IsFalse(loaded.TryGetValue(1, out _));

        loaded.Add(1, "added");

        Assert.IsTrue(loaded.TryGetValue(1, out var value));
        Assert.AreEqual("added", value);
    }

    [TestMethod]
    [DataRow(NameFileFlags.None)]
    [DataRow(NameFileFlags.Compressed | NameFileFlags.Checksum)]
    public void RoundTrip_HandlesKeysAndNamesAcrossBlocks(NameFileFlags flags)
    {
        var source = new NameTable("test");
        var key = new byte[1024 * 1024 + 17];
        new Random(817).NextBytes(key);
        var name = new string('狐', 400000) + "🦊";
        source.Add(key, name);
        source.Add(1, name);
        source.Add(2, "small");
        using var stream = new MemoryStream();
        NameFile.SaveBinary(stream, source, flags);
        stream.Position = 0;
        var loaded = NameFile.LoadBinary(stream);

        Assert.IsTrue(loaded.TryGetValue(key, out var actual));
        Assert.AreEqual(name, actual);
        Assert.IsTrue(loaded.TryGetValue(1, out actual));
        Assert.AreEqual(name, actual);
        Assert.IsTrue(loaded.TryGetValue(2, out actual));
        Assert.AreEqual("small", actual);
    }

    [TestMethod]
    [DataRow(NameFileFlags.None)]
    [DataRow(NameFileFlags.Compressed | NameFileFlags.Checksum)]
    public void RoundTrip_SupportsNonSeekableStreamsAndShortReads(NameFileFlags flags)
    {
        var source = new NameTable("test");
        source.Add(1, "name");
        using var output = new NonSeekableNameStream();
        NameFile.SaveBinary(output, source, flags);
        output.WriteByte(173);
        using var input = new NonSeekableNameStream(output.ToArray());
        var loaded = NameFile.LoadBinary(input);

        Assert.IsTrue(loaded.TryGetValue(1, out var value));
        Assert.AreEqual("name", value);
        Assert.AreEqual(173, input.ReadByte());
    }

    [TestMethod]
    public void LoadedTable_SupportsConcurrentLookups()
    {
        var source = new NameTable("test");

        for (var i = 0; i < 10000; i++)
            source.Add(i, $"name/{i}");

        using var stream = new MemoryStream();
        NameFile.SaveBinary(stream, source, NameFileFlags.Compressed);
        stream.Position = 0;
        var loaded = NameFile.LoadBinary(stream);

        Parallel.For(0, 10000, i =>
        {
            Assert.IsTrue(loaded.TryGetValue(i, out var value));
            Assert.AreEqual($"name/{i}", value);
        });
    }

    [TestMethod]
    [DataRow("0101FFFFFFFF", "", 1)]
    [DataRow("010101000000", "0161", 1)]
    [DataRow("0A0100000000", "0161", 1)]
    [DataRow("FFFFFFFF0F00", "0161", 1)]
    [DataRow("010100000000", "00", 1)]
    [DataRow("010100000000", "01FF", 1)]
    [DataRow("0101000000", "0161", 1)]
    [DataRow("010100000000010100000000", "0161", 2)]
    [DataRow("01010000000000", "0161", 1)]
    [DataRow("010100000000", "01610162", 1)]
    public void Load_RejectsInvalidEntryData(string keyHex, string nameHex, int count)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        var keys = Convert.FromHexString(keyHex);
        var names = Convert.FromHexString(nameHex);
        writer.Write(0x454C494648534148UL);
        writer.Write((ulong)NameFileFlags.None);
        writer.Write7BitEncodedInt(count);
        writer.Write("test");
        writer.Write7BitEncodedInt(keys.Length);
        writer.Write7BitEncodedInt(names.Length);
        writer.Write7BitEncodedInt(keys.Length);
        writer.Write7BitEncodedInt(keys.Length);
        writer.Write(keys);

        if (names.Length > 0)
        {
            writer.Write7BitEncodedInt(names.Length);
            writer.Write7BitEncodedInt(names.Length);
            writer.Write(names);
        }

        stream.Position = 0;

        Assert.ThrowsExactly<InvalidDataException>(() => NameFile.LoadBinary(stream));
    }

    [TestMethod]
    [DataRow(NameFileFlags.Checksum)]
    [DataRow(NameFileFlags.Compressed | NameFileFlags.Checksum)]
    public void Load_RejectsChecksumMismatchAndTruncation(NameFileFlags flags)
    {
        var source = new NameTable("test");
        source.Add(1, "name");
        using var stream = new MemoryStream();
        NameFile.SaveBinary(stream, source, flags);
        var bytes = stream.ToArray();
        bytes[^1] ^= 1;
        using var corrupt = new MemoryStream(bytes);
        using var truncated = new MemoryStream(bytes, 0, bytes.Length - 1);

        Assert.ThrowsExactly<InvalidDataException>(() => NameFile.LoadBinary(corrupt));
        Assert.ThrowsExactly<EndOfStreamException>(() => NameFile.LoadBinary(truncated));
    }

    [TestMethod]
    [DataRow(NameFileFlags.None, 0, 6)]
    [DataRow(NameFileFlags.None, 7, 6)]
    [DataRow(NameFileFlags.None, 6, 5)]
    [DataRow(NameFileFlags.Compressed, 6, 1000)]
    [DataRow(NameFileFlags.Compressed, 6, 0)]
    public void Load_RejectsInvalidBlockSizes(NameFileFlags flags, int decodedSize, int storedSize)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write(0x454C494648534148UL);
        writer.Write((ulong)flags);
        writer.Write7BitEncodedInt(1);
        writer.Write("test");
        writer.Write7BitEncodedInt(6);
        writer.Write7BitEncodedInt(2);
        writer.Write7BitEncodedInt(decodedSize);
        writer.Write7BitEncodedInt(storedSize);
        stream.Position = 0;

        Assert.ThrowsExactly<InvalidDataException>(() => NameFile.LoadBinary(stream));
    }

    [TestMethod]
    public void TextFormats_RoundTripThroughPackedStorage()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var csvPath = Path.Combine(directory, "names(test).csv");
            var txtPath = Path.Combine(directory, "names(test).txt");
            var source = new NameTable("test");
            source.Add(new NameKey(Encoding.UTF8.GetBytes("狐狸")), "狐狸");
            source.Add(new NameKey(Encoding.UTF8.GetBytes("🦊")), "🦊");
            NameFile.Save(csvPath, source);
            NameFile.Save(txtPath, source);
            var csv = NameFile.Load(csvPath);
            var txt = NameFile.Load(txtPath, "test", name => new NameKey(Encoding.UTF8.GetBytes(name.ToString())));

            CollectionAssert.AreEquivalent(source.ToArray(), csv.ToArray());
            CollectionAssert.AreEquivalent(source.ToArray(), txt.ToArray());
            Assert.AreEqual("test", csv.Name);
            Assert.AreEqual("test", txt.Name);
        }
        finally
        {
            File.Delete(Path.Combine(directory, "names(test).csv"));
            File.Delete(Path.Combine(directory, "names(test).txt"));
            Directory.Delete(directory);
        }
    }

    [TestMethod]
    public void Compressor_RejectsTrailingDataAndIncorrectOutputSize()
    {
        var input = Encoding.UTF8.GetBytes(new string('a', 100));
        var compressed = new byte[NameFileCompressor.GetMaxCompressedSize(input.Length)];
        var size = NameFileCompressor.Compress(input, compressed);
        var decoded = new byte[input.Length];

        Assert.AreEqual(input.Length, NameFileCompressor.Decompress(compressed.AsSpan(0, size), decoded));
        CollectionAssert.AreEqual(input, decoded);
        Assert.ThrowsExactly<InvalidDataException>(() => NameFileCompressor.Decompress(compressed.AsSpan(0, size + 1), decoded));
        Assert.ThrowsExactly<InvalidDataException>(() => NameFileCompressor.Decompress(compressed.AsSpan(0, size), new byte[10]));
    }
}
