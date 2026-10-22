using System.Collections;
using RedFox.GameExtraction.Hashing;

namespace RedFox.GameExtraction.Tests.NameTables;

[TestClass]
public sealed class NameTableTests
{
    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(4)]
    [DataRow(8)]
    [DataRow(16)]
    [DataRow(17)]
    [DataRow(256)]
    [DataRow(1024)]
    public void Add_CopiesKeyAndOverwritesValue(int length)
    {
        var bytes = Enumerable.Repeat((byte)17, length).ToArray();
        var key = new NameKey(bytes);
        var table = new NameTable("test");
        table.Add(bytes, "characters/狐狸/🦊.model");
        Array.Fill(bytes, (byte)33);

        Assert.IsTrue(table.TryGetValue(key, out var value));
        Assert.AreEqual("characters/狐狸/🦊.model", value);

        table.Add(key, "replacement");

        Assert.AreEqual(1, table.Count);
        Assert.IsTrue(table.TryGetValue(key, out value));
        Assert.AreEqual("replacement", value);
    }

    [TestMethod]
    public void Lookup_PreservesExactKeyBytes()
    {
        var table = new NameTable("test");
        table.Add(Array.Empty<byte>(), "empty");
        table.Add(new byte[] { 0 }, "zero");
        table.Add(new byte[] { 0, 1 }, "padded");
        table.Add(1UL, "numeric");

        Assert.IsTrue(table.TryGetValue(default(NameKey), out var empty));
        Assert.AreEqual("empty", empty);
        Assert.IsTrue(table.TryGetValue(new byte[] { 0 }, out var zero));
        Assert.AreEqual("zero", zero);
        Assert.IsTrue(table.TryGetValue(new byte[] { 0, 1 }, out var padded));
        Assert.AreEqual("padded", padded);
        Assert.IsTrue(table.TryGetValue(1U, out var numeric));
        Assert.AreEqual("numeric", numeric);
        Assert.IsFalse(table.TryGetValue(2UL, out var missing));
        Assert.IsNull(missing);
    }

    [TestMethod]
    public void GrowthAndCompaction_PreserveMappings()
    {
        var table = new NameTable("test");
        var expected = new Dictionary<NameKey, string>();
        var random = new Random(8731);

        for (var i = 0; i < 20000; i++)
        {
            var key = new NameKey((ulong)random.Next(12000));
            var value = $"name/{random.Next(500)}/狐狸";
            table.Add(key, value);
            expected[key] = value;

            if (i % 2000 == 0)
                table.TrimExcess();
        }

        table.TrimExcess();

        Assert.AreEqual(expected.Count, table.Count);

        foreach (var (key, name) in expected)
        {
            Assert.IsTrue(table.TryGetValue(key, out var actual));
            Assert.AreEqual(name, actual);
        }

        CollectionAssert.AreEquivalent(expected.ToArray(), table.ToArray());
        Assert.AreEqual(expected.Count, ((IEnumerable)table).Cast<KeyValuePair<NameKey, string>>().Count());
    }

    [TestMethod]
    public void OverwriteSharedValue_DoesNotChangeOtherKeys()
    {
        var table = new NameTable("test");
        table.Add(1, "shared");
        table.Add(2, "shared");
        table.TrimExcess();
        table.Add(1, "changed");
        table.TrimExcess();

        Assert.IsTrue(table.TryGetValue(1, out var changed));
        Assert.AreEqual("changed", changed);
        Assert.IsTrue(table.TryGetValue(2, out var shared));
        Assert.AreEqual("shared", shared);
    }

    [TestMethod]
    public void MissingByteArrayLookup_DoesNotAllocate()
    {
        var table = new NameTable("test");
        table.Add(1, "present");
        var key = new byte[64];
        table.TryGetValue(key, out _);
        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var i = 0; i < 1000; i++)
            table.TryGetValue(key, out _);

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.AreEqual(0L, allocated);
    }

    [TestMethod]
    public void Clear_AllowsReuseAndPreservesMetadata()
    {
        var table = new NameTable("test");
        table.Metadata["Game"] = "example";
        table.Add(1, "before");
        table.Clear();
        table.TrimExcess();

        Assert.AreEqual(0, table.Count);
        Assert.IsFalse(table.TryGetValue(1, out _));
        Assert.AreEqual(0, table.Count());
        Assert.AreEqual("example", table.Metadata["Game"]);

        table.Add(2, "after");

        Assert.IsTrue(table.TryGetValue(2, out var value));
        Assert.AreEqual("after", value);
    }

    [TestMethod]
    public void Add_RejectsInvalidValuesWithoutChangingTable()
    {
        var table = new NameTable("test");
        table.Add(1, "original");

        Assert.ThrowsExactly<ArgumentNullException>(() => table.Add(1, null!));
        Assert.ThrowsExactly<ArgumentException>(() => table.Add(1, " \t\u2003"));
        Assert.ThrowsExactly<ArgumentNullException>(() => table.Add((byte[])null!, "value"));
        Assert.ThrowsExactly<ArgumentNullException>(() => table.TryGetValue((byte[])null!, out _));
        Assert.AreEqual(1, table.Count);
        Assert.IsTrue(table.TryGetValue(1, out var value));
        Assert.AreEqual("original", value);
    }
}
