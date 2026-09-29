using RedFox.GameExtraction.CommandLine;

namespace RedFox.GameExtraction.Tests;

[TestClass]
public sealed class CommandLineTokenizerTests
{
    [TestMethod]
    public void Tokenize_SplitsOnWhitespace()
    {
        CollectionAssert.AreEqual(new[] { "/set", "Overwrite", "true" }, CommandLineTokenizer.Tokenize("  /set   Overwrite true ").ToArray());
    }

    [TestMethod]
    public void Tokenize_KeepsQuotedSpacesAndBackslashes()
    {
        CollectionAssert.AreEqual(new[] { "/mount", @"C:\Program Files\Game\data.pak" }, CommandLineTokenizer.Tokenize(@"/mount ""C:\Program Files\Game\data.pak""").ToArray());
    }

    [TestMethod]
    public void Tokenize_KeepsEmptyQuotedToken()
    {
        CollectionAssert.AreEqual(new[] { "/set", "Name", "" }, CommandLineTokenizer.Tokenize(@"/set Name """"").ToArray());
    }

    [TestMethod]
    public void GetTokenBounds_ReturnsTokenAtCaret()
    {
        string line = "/set Overwrite tr";

        Assert.AreEqual((15, 17), CommandLineTokenizer.GetTokenBounds(line, line.Length));
        Assert.AreEqual((5, 14), CommandLineTokenizer.GetTokenBounds(line, 7));
    }

    [TestMethod]
    public void GetTokenBounds_IncludesQuotedSpaces()
    {
        string line = @"/mount ""C:\Program Fi";

        Assert.AreEqual((7, line.Length), CommandLineTokenizer.GetTokenBounds(line, line.Length));
    }

    [TestMethod]
    public void GroupArguments_MountsLeadingPathsThenRunsCommands()
    {
        string[] arguments = ["a.zip", "b.zip", "/extract", "*.png", "/exit"];

        IReadOnlyList<IReadOnlyList<string>> invocations = CommandLineTokenizer.GroupArguments(arguments, name => name is "extract" or "exit");

        Assert.HasCount(3, invocations);
        CollectionAssert.AreEqual(new[] { "mount", "a.zip", "b.zip" }, invocations[0].ToArray());
        CollectionAssert.AreEqual(new[] { "extract", "*.png" }, invocations[1].ToArray());
        CollectionAssert.AreEqual(new[] { "exit" }, invocations[2].ToArray());
    }

    [TestMethod]
    public void GroupArguments_TreatsUnknownSlashTokensAsPaths()
    {
        string[] arguments = ["/home/user/game.zip"];

        IReadOnlyList<IReadOnlyList<string>> invocations = CommandLineTokenizer.GroupArguments(arguments, name => name is "extract");

        Assert.HasCount(1, invocations);
        CollectionAssert.AreEqual(new[] { "mount", "/home/user/game.zip" }, invocations[0].ToArray());
    }
}
