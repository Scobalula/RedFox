using RedFox.Avalonia.CodeEditor;

namespace RedFox.Tests.CodeEditor;

public sealed class WordOccurrencesTests
{
    [Theory]
    [InlineData("bbenable mpxp", 2, "bbenable")]
    [InlineData("bbenable mpxp", 8, "bbenable")]
    [InlineData("bbenable mpxp", 9, "mpxp")]
    [InlineData("a  b", 2, "")]
    [InlineData("", 0, "")]
    public void FindsTheWordAtTheCaret(string line, int column, string word)
    {
        Assert.Equal(word, WordOccurrences.FindWord(line, column));
    }

    [Theory]
    [InlineData("\t\tcode", 0, 0, 2)]
    [InlineData("\t\tcode", 1, 0, 2)]
    [InlineData("\t\tcode", 3, 2, 4)]
    [InlineData("    code", 2, 0, 4)]
    [InlineData("a  \t b", 2, 1, 4)]
    [InlineData("//=========", 5, 0, 11)]
    [InlineData("------- x", 3, 0, 7)]
    [InlineData("a == b", 2, 2, 2)]
    [InlineData("foo();", 4, 3, 3)]
    [InlineData("code", 9, 0, 4)]
    [InlineData("", 0, 0, 0)]
    public void DoubleClickRunsAreFound(string line, int column, int start, int length)
    {
        Assert.Equal((start, length), WordOccurrences.FindRun(line, column));
    }

    [Fact]
    public void WholeWordSkipsLongerWords()
    {
        Assert.Equal([0, 12], WordOccurrences.Find("room rooms, room", "room", true));
    }

    [Fact]
    public void ASelectionMatchesInsideWords()
    {
        Assert.Equal([0, 5, 12], WordOccurrences.Find("room rooms, room", "room", false));
    }
}
