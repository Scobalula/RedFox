using RedFox.GameExtraction.CommandLine;

namespace RedFox.GameExtraction.Tests;

[TestClass]
public sealed class SettingValueParserTests
{
    [TestMethod]
    [DataRow("true", "True")]
    [DataRow("YES", "True")]
    [DataRow("on", "True")]
    [DataRow("1", "True")]
    [DataRow("false", "False")]
    [DataRow("No", "False")]
    [DataRow("off", "False")]
    [DataRow("0", "False")]
    public void Parse_Boolean_NormalizesBooleans(string input, string expected)
    {
        GameExtractionSetting setting = new() { Name = "Overwrite", Type = GameExtractionSettingType.Boolean };

        Assert.AreEqual(expected, SettingValueParser.Parse(setting, input));
    }

    [TestMethod]
    public void Parse_Boolean_RejectsUnknownValues()
    {
        GameExtractionSetting setting = new() { Name = "Overwrite", Type = GameExtractionSettingType.Boolean };

        Assert.ThrowsExactly<ArgumentException>(() => SettingValueParser.Parse(setting, "maybe"));
    }

    [TestMethod]
    public void Parse_Choice_ReturnsCanonicalOption()
    {
        GameExtractionSetting setting = new() { Name = "ImageFormat", Type = GameExtractionSettingType.Choice, Options = [".dds", ".PNG"] };

        Assert.AreEqual(".PNG", SettingValueParser.Parse(setting, ".png"));
        Assert.ThrowsExactly<ArgumentException>(() => SettingValueParser.Parse(setting, ".tga"));
    }

    [TestMethod]
    public void Parse_DirectoryPath_ReturnsFullPath()
    {
        GameExtractionSetting setting = new() { Name = "OutputDirectory", Type = GameExtractionSettingType.DirectoryPath };

        Assert.AreEqual(Path.GetFullPath("exports"), SettingValueParser.Parse(setting, "exports"));
    }

    [TestMethod]
    public void Parse_Text_ReturnsInput()
    {
        GameExtractionSetting setting = new() { Name = "Formats", Type = GameExtractionSettingType.Text };

        Assert.AreEqual(".semodel, .cast", SettingValueParser.Parse(setting, ".semodel, .cast"));
    }
}
