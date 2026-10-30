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
    public void Matches_ValueConditions_CompareNumerically()
    {
        ModelAsset model = new("models/arm.xmodel", "XModel", null)
        {
            BoneCount = 60,
            LodCount = 2,
        };

        Assert.IsTrue(AssetFilter.Parse(["bonecount:>50"]).Matches(model));
        Assert.IsFalse(AssetFilter.Parse(["bonecount:<60"]).Matches(model));
        Assert.IsTrue(AssetFilter.Parse(["LodCount:2"]).Matches(model));
        Assert.IsFalse(AssetFilter.Parse(["width:>0"]).Matches(model));
    }

    [TestMethod]
    public void Matches_NameCondition_SearchesBoneNames()
    {
        ModelAsset model = new("models/arm.xmodel", "XModel", null) { BoneNames = ["tag_origin", "j_elbow_le"] };

        Assert.IsTrue(AssetFilter.Parse(["bonename:ELBOW"]).Matches(model));
        Assert.IsFalse(AssetFilter.Parse(["bonename:knee"]).Matches(model));
    }

    [TestMethod]
    public void Matches_MetadataValue_UsedWithoutSubclass()
    {
        Asset asset = new("sounds/a.wav", "WAV", null, null, new Dictionary<string, object?> { ["Streamed"] = true });

        Assert.IsTrue(AssetFilter.Parse(["streamed:1"]).Matches(asset));
        Assert.IsFalse(AssetFilter.Parse(["streamed:0"]).Matches(asset));
    }

    [TestMethod]
    public void Matches_Exclusion_RejectsMatchingNames()
    {
        AssetFilter filter = AssetFilter.Parse(["rock", "!.png"]);

        Assert.IsFalse(filter.Matches(Texture));
        Assert.IsTrue(filter.Matches(Model));
    }

    [TestMethod]
    public void CompareInformation_ModelAsset_SortsBoneCountsNumerically()
    {
        ModelAsset nine	= new("a", "XModel", null) { BoneCount = 9 };
        ModelAsset ten	= new("b", "XModel", null) { BoneCount = 10 };

        Assert.IsLessThan(0, nine.CompareInformation(ten));
    }

    [TestMethod]
    public void CompareInformation_PlainAsset_SortsNumbersInText()
    {
        Asset nine	= new("a", "material", null, "Images: 9");
        Asset ten	= new("b", "material", null, "Images: 10");

        Assert.IsLessThan(0, nine.CompareInformation(ten));
    }

    [TestMethod]
    public void GetCompletions_ReturnsDistinctSortedTypes()
    {
        string[] completions = [.. AssetFilter.GetCompletions([Texture, Model, new Asset("b.png", "png")])];

        CollectionAssert.AreEqual(new[] { "type:PNG", "type:SEModel" }, completions);
    }
}
