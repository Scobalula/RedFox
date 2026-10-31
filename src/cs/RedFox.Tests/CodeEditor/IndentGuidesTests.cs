using RedFox.Avalonia.CodeEditor;

namespace RedFox.Tests.CodeEditor;

public sealed class IndentGuidesTests
{
    [Theory]
    [InlineData("", 0)]
    [InlineData("code", 4)]
    [InlineData("\tcode", 8)]
    [InlineData("a\tb", 5)]
    [InlineData("\t\t", 8)]
    public void ColumnCountExpandsTabs(string line, int columns)
    {
        Assert.Equal(columns, IndentGuides.GetColumnCount(line, 4));
    }

    [Theory]
    [InlineData("code", 0)]
    [InlineData("    code", 1)]
    [InlineData("\t\tcode", 2)]
    [InlineData("  \tcode", 1)]
    [InlineData("   code", 0)]
    [InlineData("", -1)]
    [InlineData("  \t ", -1)]
    public void LevelCountsLeadingIndentation(string line, int level)
    {
        Assert.Equal(level, IndentGuides.GetLevel(line, 4));
    }
}
