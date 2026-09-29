using RedFox.GameExtraction.CommandLine;

namespace RedFox.GameExtraction.Tests;

[TestClass]
public sealed class AssetFilterTests
{
    private static readonly Asset Texture = new("textures/env/Rock_01.png", "PNG");

    private static readonly Asset Model = new("models/rock.semodel", "SEModel");

    [TestMethod]
    public void Parse_WithoutArguments_IsEmpty()
    {
        AssetFilter filter = AssetFilter.Parse([]);

        Assert.IsTrue(filter.IsEmpty);
        Assert.IsTrue(filter.Matches(Texture));
    }

    [TestMethod]
    public void Matches_PlainPattern_UsesCaseInsensitiveContains()
    {
        AssetFilter filter = AssetFilter.Parse(["ROCK"]);

        Assert.IsTrue(filter.Matches(Texture));
        Assert.IsTrue(filter.Matches(Model));
    }

    [TestMethod]
    public void Matches_WildcardPattern_MatchesWholeName()
    {
        AssetFilter filter = AssetFilter.Parse(["*.png"]);

        Assert.IsTrue(filter.Matches(Texture));
        Assert.IsFalse(filter.Matches(Model));
        Assert.IsFalse(AssetFilter.Parse(["rock*"]).Matches(Texture));
    }

    [TestMethod]
    public void Matches_TypeFilter_IsCaseInsensitive()
    {
        AssetFilter filter = AssetFilter.Parse(["type:semodel"]);

        Assert.AreEqual("semodel", filter.Type);
        Assert.IsNull(filter.Pattern);
        Assert.IsFalse(filter.Matches(Texture));
        Assert.IsTrue(filter.Matches(Model));
    }

    [TestMethod]
    public void Matches_PatternAndType_RequiresBoth()
    {
        AssetFilter filter = AssetFilter.Parse(["rock", "type:PNG"]);

        Assert.IsTrue(filter.Matches(Texture));
        Assert.IsFalse(filter.Matches(Model));
    }

    [TestMethod]
    public void GetCompletions_ReturnsDistinctSortedTypes()
    {
        string[] completions = [.. AssetFilter.GetCompletions([Texture, Model, new Asset("b.png", "png")])];

        CollectionAssert.AreEqual(new[] { "type:PNG", "type:SEModel" }, completions);
    }
}
