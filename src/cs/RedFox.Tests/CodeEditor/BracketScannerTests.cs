using RedFox.Avalonia.CodeEditor;

namespace RedFox.Tests.CodeEditor;

public sealed class BracketScannerTests
{
    private const string BlockCommentOpen = "/* { ";
    private const string BlockCommentClose = "*/";

    [Fact]
    public void NestedBracketsCarryTheirDepth()
    {
        List<BracketToken> brackets = BracketScanner.Scan("{ ( [ ] ) }", BracketSyntax.CStyle);

        Assert.Equal([0, 1, 2, 2, 1, 0], brackets.Select(bracket => bracket.Depth));
    }

    [Theory]
    [InlineData("// {\n")]
    [InlineData(BlockCommentOpen + BlockCommentClose)]
    [InlineData("\"{\"")]
    [InlineData("\"\\\"{\"")]
    public void CommentsAndStringsAreSkipped(string text)
    {
        Assert.Empty(BracketScanner.Scan(text, BracketSyntax.CStyle));
    }

    [Fact]
    public void LuaCommentsAndSingleQuotesAreSkipped()
    {
        Assert.Empty(BracketScanner.Scan("-- {\n--[[ ( ]]\nlocal a = '['\n", BracketSyntax.Lua));
    }

    [Fact]
    public void AnUnterminatedStringStopsAtItsLine()
    {
        Assert.Single(BracketScanner.Scan("\"open\n{", BracketSyntax.CStyle));
    }

    [Fact]
    public void AnUnmatchedCloseDoesNotGoNegative()
    {
        Assert.Equal(0, BracketScanner.Scan("}", BracketSyntax.CStyle)[0].Depth);
    }

    [Theory]
    [InlineData("a.gsc", true)]
    [InlineData("a.json", true)]
    [InlineData("a.lua", true)]
    [InlineData("a.csv", false)]
    [InlineData("a", false)]
    public void SyntaxFollowsTheFileType(string fileName, bool hasSyntax)
    {
        Assert.Equal(hasSyntax, BracketSyntax.ForFile(fileName) is not null);
    }
}
