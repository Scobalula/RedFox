using System.Buffers.Binary;
using System.IO.MemoryMappedFiles;
using System.Text;
using RedFox.GameExtraction.Hashing;

namespace RedFox.GameExtraction.Tests.NameTables;

[TestClass]
public sealed class MappedNameTableTests
{
    private string _directory = string.Empty;

    [TestInitialize]
    public void Initialize()
    {
        _directory = Path.Combine(Path.GetTempPath(), "RedFox.NameMaps", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_directory, true);

    [TestMethod]
    public void RoundTrip_PreservesExactKeysNamesMetadataAndSource()
    {
        var source = new NameTable("test");
        source.Metadata["Game"] = "狐狸";
        source.Add(Array.Empty<byte>(), "empty");
        source.Add(new byte[] { 0 }, "zero");
        source.Add(new byte[] { 0, 1 }, "padded");
        source.Add(1UL, "numeric");
        source.Add(new byte[1024], "characters/狐狸/🦊.model");
        source.Add(2, "characters/狐狸/🦊.model");
        source.Add(3, "old");
        source.Add(3, "replacement");
        var original	= source.ToArray();
        var path		= Path.Combine(_directory, "names.namemap");
        NameFile.SaveMapped(path, source);
        using var mapped = NameFile.OpenMapped(path);

        Assert.IsTrue(mapped.IsReadOnly);
        Assert.IsFalse(source.IsReadOnly);
        Assert.AreEqual(source.Count, mapped.Count);
        Assert.AreEqual("test", mapped.Name);
        Assert.AreEqual("狐狸", mapped.Metadata["Game"]);
        CollectionAssert.AreEquivalent(original, mapped.ToArray());
        CollectionAssert.AreEqual(original, source.ToArray());

        foreach (var (key, value) in original)
        {
            Assert.IsTrue(mapped.TryGetValue(key, out var actual));
            Assert.AreEqual(value, actual);
            Assert.IsTrue(mapped.TryGetValue(key.Span.ToArray(), out actual));
            Assert.AreEqual(value, actual);
            Assert.IsTrue(mapped.TryGetValue(key.Span, out actual));
            Assert.AreEqual(value, actual);
        }

        Assert.IsFalse(mapped.TryGetValue(4, out var missing));
        Assert.IsNull(missing);
        if (OperatingSystem.IsWindows())
            Assert.ThrowsExactly<IOException>(() => File.Open(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite).Dispose());
        mapped.TrimExcess();
        Assert.ThrowsExactly<NotSupportedException>(() => mapped.Add(4, "new"));
        Assert.ThrowsExactly<NotSupportedException>(mapped.Clear);
        mapped.Name = "renamed";

        mapped.Metadata["Extra"] = "metadata";

        using var binary = new MemoryStream();
        NameFile.SaveBinary(binary, mapped, NameFileFlags.Compressed | NameFileFlags.Checksum);
        binary.Position = 0;

        var loaded = NameFile.LoadBinary(binary);

        Assert.AreEqual("renamed", loaded.Name);
        Assert.AreEqual("metadata", loaded.Metadata["Extra"]);
        CollectionAssert.AreEquivalent(original, loaded.ToArray());

        using var csv = new MemoryStream();
        using var txt = new MemoryStream();
        NameFile.SaveCsv(csv, mapped);
        NameFile.SaveTxt(txt, mapped);
        Assert.IsTrue(Encoding.UTF8.GetString(csv.ToArray()).Contains("characters/狐狸/🦊.model", StringComparison.Ordinal));
        Assert.IsTrue(Encoding.UTF8.GetString(txt.ToArray()).Contains("replacement", StringComparison.Ordinal));

        var copy = Path.Combine(_directory, "copy.namemap");
        NameFile.SaveMapped(copy, mapped);
        using var reopened = NameFile.OpenMapped(copy);
        CollectionAssert.AreEquivalent(original, reopened.ToArray());
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void PublicStorage_SupportsUnalignedViewsAndExplicitOwnership(bool leaveOpen)
    {
        var path		= WriteSingleEntry();
        var bytes		= File.ReadAllBytes(path);
        var embedded	= Path.Combine(_directory, "embedded.bin");

        using (var stream = File.Create(embedded))
        {
            stream.Write(new byte[17]);
            stream.Write(bytes);
            stream.Write(new byte[19]);
        }

        using var mapping = MemoryMappedFile.CreateFromFile(embedded, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
        using var view = mapping.CreateViewAccessor(17, bytes.Length, MemoryMappedFileAccess.Read);
        var storage = new MappedNameTableStorage(view, leaveOpen);
        using var table = new NameTable(storage);

        Assert.AreEqual(1, storage.Count);
        Assert.AreEqual(16, storage.BucketCount);
        Assert.AreEqual(6, storage.Keys.Length);
        Assert.AreEqual(5, storage.Names.Length);
        Assert.AreEqual(1, storage.ReadBucket(12));
        Assert.IsTrue(table.TryGetValue(1, out var value));
        Assert.AreEqual("name", value);

        table.Dispose();
        Assert.ThrowsExactly<ObjectDisposedException>(() => storage.ReadBucket(12));

        if (leaveOpen)
            Assert.AreEqual((byte)'R', view.ReadByte(0));
        else
            Assert.ThrowsExactly<ObjectDisposedException>(() => view.ReadByte(0));

        using var otherView = mapping.CreateViewAccessor(17, bytes.Length, MemoryMappedFileAccess.Read);
        Assert.AreEqual((byte)'R', otherView.ReadByte(0));
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void PublicStorage_FileStreamConstructorPreservesPositionAndOwnership(bool leaveOpen)
    {
        var path = WriteSingleEntry();
        using var stream = File.OpenRead(path);
        stream.Position = 7;
        using var storage = new MappedNameTableStorage(stream, leaveOpen);
        using var table = new NameTable(storage);

        Assert.AreEqual(7L, stream.Position);
        Assert.IsTrue(table.TryGetValue(1, out var value));
        Assert.AreEqual("name", value);

        table.Dispose();

        if (leaveOpen)
            Assert.AreEqual(7L, stream.Position);
        else
            Assert.ThrowsExactly<ObjectDisposedException>(() => stream.ReadByte());
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void PublicStorage_FileStreamConstructorFailureLeavesStreamOpen(bool leaveOpen)
    {
        var path = Path.Combine(_directory, "invalid.namemap");
        File.WriteAllBytes(path, new byte[52]);
        using var stream = File.OpenRead(path);

        Assert.ThrowsExactly<InvalidDataException>(() => new MappedNameTableStorage(stream, leaveOpen));
        Assert.AreEqual(0, stream.ReadByte());
    }

    [TestMethod]
    public void PublicStorage_OpenTransfersLifetimeToTable()
    {
        var path	= WriteSingleEntry();
        var storage	= MappedNameTableStorage.Open(path);
        using var table = new NameTable(storage);

        Assert.IsTrue(table.TryGetValue(1, out _));
        table.Dispose();
        Assert.ThrowsExactly<ObjectDisposedException>(() => storage.ReadBucket(0));

        using var exclusive = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [TestMethod]
    public void PublicStorage_RejectsMalformedViewWithoutTakingOwnership()
    {
        using var mapping = MemoryMappedFile.CreateNew(null, 52);
        using var view = mapping.CreateViewAccessor(0, 52, MemoryMappedFileAccess.ReadWrite);

        Assert.ThrowsExactly<InvalidDataException>(() => new MappedNameTableStorage(view, false));
        view.Write(0, (byte)17);
        Assert.AreEqual((byte)17, view.ReadByte(0));
    }

    [TestMethod]
    public void EmptyTable_IsReadOnlyAndChecksDisposal()
    {
        var path = Path.Combine(_directory, "empty.namemap");
        NameFile.SaveMapped(path, new NameTable("empty"));
        var mapped = NameFile.OpenMapped(path);

        Assert.AreEqual(0, mapped.Count);
        Assert.IsEmpty(mapped.ToArray());
        Assert.IsFalse(mapped.TryGetValue(1, out _));
        mapped.TrimExcess();
        mapped.Dispose();
        mapped.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => mapped.TryGetValue(1, out _));
        Assert.ThrowsExactly<ObjectDisposedException>(() => mapped.ToArray());
        Assert.ThrowsExactly<ObjectDisposedException>(mapped.TrimExcess);
        Assert.ThrowsExactly<ObjectDisposedException>(mapped.Clear);
        Assert.ThrowsExactly<ObjectDisposedException>(() => mapped.Add(1, "after"));

        using var exclusive = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [TestMethod]
    public void VersionOne_UsesStableLittleEndianRecordsAndFnvIndex()
    {
        var header		= Convert.FromHexString("52464E414D454D0001000000010000001000000006000000050000003D0000000000000043000000000000004800000000000000047465737400000000010100000000046E616D65");
        var expected	= new byte[136];
        header.CopyTo(expected, 0);
        expected[120] = 1;
        var path = WriteSingleEntry();

        CollectionAssert.AreEqual(expected, File.ReadAllBytes(path));
        File.WriteAllBytes(path, expected);
        using var mapped = NameFile.OpenMapped(path);
        Assert.IsTrue(mapped.TryGetValue(1, out var value));
        Assert.AreEqual("name", value);
    }

    [TestMethod]
    public void LargeRecords_CrossMappedPagesWithoutChangingValues()
    {
        var source	= new NameTable("test");
        var key		= new byte[17001];
        var name	= new string('狐', 400000) + "🦊";
        new Random(817).NextBytes(key);
        source.Add(key, name);
        source.Add(1, name);
        var path = Path.Combine(_directory, "large-records.namemap");
        NameFile.SaveMapped(path, source);
        using var mapped = NameFile.OpenMapped(path);

        Assert.IsTrue(mapped.TryGetValue(key, out var value));
        Assert.AreEqual(name, value);
        Assert.IsTrue(mapped.TryGetValue(1, out value));
        Assert.AreEqual(name, value);
    }

    [TestMethod]
    public void ConcurrentLookups_PreserveCollisionsAndAllocateNothingOnMisses()
    {
        var source = new NameTable("test");

        for (var i = 0; i < 10000; i++)
            source.Add(i, $"name/{i}");

        var path = Path.Combine(_directory, "concurrent.namemap");
        NameFile.SaveMapped(path, source);
        using var mapped = NameFile.OpenMapped(path);

        Parallel.For(0, 20000, i =>
        {
            var key = i % 10000;
            Assert.IsTrue(mapped.TryGetValue(key, out var value));
            Assert.AreEqual($"name/{key}", value);
        });

        var missing = new byte[64];
        mapped.TryGetValue(missing, out _);
        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var i = 0; i < 1000; i++)
            mapped.TryGetValue(missing, out _);

        Assert.AreEqual(0L, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [TestMethod]
    public void Opening_DoesNotAllocateInProportionToEntryCount()
    {
        var smallPath	= Path.Combine(_directory, "small.namemap");
        var largePath	= Path.Combine(_directory, "large.namemap");
        var source		= new NameTable("test");
        source.Add(0, "name/0");
        NameFile.SaveMapped(smallPath, source);

        for (var i = 1; i < 100000; i++)
            source.Add(i, $"name/{i}");

        NameFile.SaveMapped(largePath, source);
        using var warmup = NameFile.OpenMapped(smallPath);
        var before = GC.GetAllocatedBytesForCurrentThread();
        using var small = NameFile.OpenMapped(smallPath);
        var smallAllocated	= GC.GetAllocatedBytesForCurrentThread() - before;
        before				= GC.GetAllocatedBytesForCurrentThread();
        using var large = NameFile.OpenMapped(largePath);
        var largeAllocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.IsLessThan(65536L, largeAllocated);
        Assert.IsLessThan(4096L, Math.Abs(largeAllocated - smallAllocated));
        Assert.IsTrue(large.TryGetValue(99999, out var value));
        Assert.AreEqual("name/99999", value);
    }

    [TestMethod]
    public void Manager_PreservesPrecedenceAndMappingOwnership()
    {
        var path	= Path.Combine(_directory, "managed.namemap");
        var source	= new NameTable("test");
        source.Add(1, "mapped");
        NameFile.SaveMapped(path, source);
        using var manager = new NameTableManager();
        var first = manager.CreateNameTable("TEST");
        first.Add(1, "first");
        var owned = manager.OpenMapped(path);
        using var borrowed = NameFile.OpenMapped(path);
        manager.Add(borrowed);

        Assert.AreEqual(3, manager.TotalEntries);
        Assert.IsTrue(manager.TryGetValue("TeSt", 1, out var value));
        Assert.AreEqual("first", value);
        Assert.IsTrue(manager.TryGetTable("test", out var found));
        Assert.AreSame(first, found);
        Assert.IsTrue(manager.Remove(owned));
        manager.Clear();

        Assert.IsTrue(owned.TryGetValue(1, out _));
        Assert.IsTrue(borrowed.TryGetValue(1, out _));
        Assert.IsTrue(first.TryGetValue(1, out _));
        owned.Dispose();
        var closedByClear = manager.OpenMapped(path);
        manager.Clear();
        Assert.ThrowsExactly<ObjectDisposedException>(() => closedByClear.TryGetValue(1, out _));
        var closedByDispose = manager.OpenMapped(path);
        manager.Dispose();
        Assert.ThrowsExactly<ObjectDisposedException>(() => closedByDispose.TryGetValue(1, out _));
    }

    [TestMethod]
    public void DirectoryFactory_OpensOnlyMappedFilesAndReleasesThem()
    {
        var path	= Path.Combine(_directory, "names.NAMEMAP");
        var source	= new NameTable("test");
        source.Add(1, "mapped");
        NameFile.SaveMapped(path, source);
        File.WriteAllText(Path.Combine(_directory, "ignored.txt"), "ignored");
        var service = NameListService.CreateMappedFromDirectory(_directory);

        Assert.HasCount(1, service.Manager.Tables);
        var mapped = service.Manager.Tables[0];
        Assert.IsTrue(mapped.TryGetValue(1, out _));
        service.Dispose();
        Assert.ThrowsExactly<ObjectDisposedException>(() => mapped.TryGetValue(1, out _));

        using var exclusive = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [TestMethod]
    [DataRow(0, 123L)]
    [DataRow(8, 2L)]
    [DataRow(12, -1L)]
    [DataRow(16, 3L)]
    [DataRow(20, -1L)]
    [DataRow(24, -1L)]
    [DataRow(28, -1L)]
    [DataRow(36, long.MaxValue)]
    [DataRow(44, long.MaxValue)]
    public void Open_RejectsMalformedHeader(int offset, long value)
    {
        var path	= WriteSingleEntry();
        var bytes	= File.ReadAllBytes(path);

        if (offset == 0 || offset >= 28)
            BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(offset), value);
        else
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset), (int)value);

        File.WriteAllBytes(path, bytes);
        Assert.ThrowsExactly<InvalidDataException>(() => NameFile.OpenMapped(path));

        using var exclusive = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [TestMethod]
    public void Open_RejectsTruncationAndMalformedMetadata()
    {
        var path	= WriteSingleEntry();
        var bytes	= File.ReadAllBytes(path);

        foreach (var length in new[] { 0, 10, 51, bytes.Length - 1 })
        {
            File.WriteAllBytes(path, bytes.AsSpan(0, length).ToArray());
            Assert.ThrowsExactly<InvalidDataException>(() => NameFile.OpenMapped(path));
        }

        bytes[52] = 127;
        File.WriteAllBytes(path, bytes);
        Assert.ThrowsExactly<InvalidDataException>(() => NameFile.OpenMapped(path));
    }

    [TestMethod]
    [DataRow("key")]
    [DataRow("offset")]
    [DataRow("name")]
    [DataRow("utf8")]
    [DataRow("bucket")]
    [DataRow("full")]
    public void Lookup_RejectsMalformedRecordsAndTerminatesProbing(string corruption)
    {
        var path			= WriteSingleEntry();
        var bytes			= File.ReadAllBytes(path);
        var keyOffset		= (int)BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(28));
        var nameOffset		= (int)BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(36));
        var bucketOffset	= (int)BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(44));

        if (corruption == "key")
        {
            bytes[keyOffset] = 127;
        }
        else if (corruption == "offset")
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(keyOffset + 2), -1);
        }
        else if (corruption == "name")
        {
            bytes[nameOffset] = 127;
        }
        else if (corruption == "utf8")
        {
            bytes[nameOffset + 1] = 255;
        }
        else
        {
            for (var i = bucketOffset; i < bytes.Length; i += sizeof(int))
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(i), corruption == "bucket" ? int.MaxValue : 1);
        }

        File.WriteAllBytes(path, bytes);
        using var mapped = NameFile.OpenMapped(path);
        Assert.ThrowsExactly<InvalidDataException>(() => mapped.TryGetValue(corruption == "full" ? 2 : 1, out _));
    }

    [TestMethod]
    public void MappedSections_CanStartBeyondTwoGigabytes()
    {
        var path			= WriteSingleEntry();
        var bytes			= File.ReadAllBytes(path);
        var keyOffset		= BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(28));
        var nameOffset		= BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(36));
        var bucketOffset	= BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(44));
        const long shift = 1L << 31;

        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(28), keyOffset + shift);
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(36), nameOffset + shift);
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(44), bucketOffset + shift);

        using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
        {
            stream.Write(bytes.AsSpan(0, (int)keyOffset));
            stream.Position = keyOffset + shift;
            stream.Write(bytes.AsSpan((int)keyOffset));
        }

        using var mapped = NameFile.OpenMapped(path);
        Assert.IsTrue(mapped.TryGetValue(1, out var value));
        Assert.AreEqual("name", value);
    }

    [TestMethod]
    public void FailedSave_PreservesDestinationAndRemovesTemporaryFile()
    {
        var path		= WriteSingleEntry();
        var original	= File.ReadAllBytes(path);
        var disposed	= NameFile.OpenMapped(path);
        disposed.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => NameFile.SaveMapped(path, disposed));
        CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
        Assert.HasCount(1, Directory.GetFiles(_directory));
        Assert.ThrowsExactly<ArgumentException>(() => NameFile.Save(path, new NameTable("test"), NameFileFlags.Compressed));
        CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
    }

    private string WriteSingleEntry()
    {
        var path	= Path.Combine(_directory, "single.namemap");
        var source	= new NameTable("test");
        source.Add(1, "name");
        NameFile.SaveMapped(path, source);
        return path;
    }
}
