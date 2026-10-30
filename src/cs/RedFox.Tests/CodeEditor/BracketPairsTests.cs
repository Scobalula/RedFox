using RedFox.Avalonia.CodeEditor;

namespace RedFox.Tests.CodeEditor;

public sealed class BracketPairsTests
{
    [Fact]
    public void NestedBracketsPairWithTheirOwnPartner()
    {
        Dictionary<int, int> pairs = Pairs("{ ( ) }");

        Assert.Equal(6, pairs[0]);
        Assert.Equal(0, pairs[6]);
        Assert.Equal(4, pairs[2]);
    }

    [Fact]
    public void BracketsInCommentsAndStringsArePassedOver()
    {
        Dictionary<int, int> pairs = Pairs("{ \"}\" // }\n}");

        Assert.Equal(11, pairs[0]);
    }

    [Fact]
    public void AnUnmatchedBracketHasNoPartner()
    {
        Assert.Empty(Pairs("{ )"));
    }

    [Theory]
    [InlineData(0, 0, 3)]
    [InlineData(1, 0, 3)]
    [InlineData(3, 3, 0)]
    [InlineData(4, 3, 0)]
    public void TheCaretFindsABracketOnEitherSide(int caret, int bracket, int partner)
    {
        Assert.True(BracketPairs.TryFind(Pairs("{  }"), caret, out int foundBracket, out int foundPartner));
        Assert.Equal(bracket, foundBracket);
        Assert.Equal(partner, foundPartner);
    }

    [Fact]
    public void TheBracketBeforeTheCaretComesFirst()
    {
        Assert.True(BracketPairs.TryFind(Pairs("(){}"), 2, out int bracket, out _));
        Assert.Equal(1, bracket);
    }

    [Fact]
    public void ACaretAwayFromBracketsFindsNothing()
    {
        Assert.False(BracketPairs.TryFind(Pairs("{    }"), 3, out _, out _));
    }

    private static Dictionary<int, int> Pairs(string text)
    {
        return BracketPairs.Build(BracketScanner.Scan(text, BracketSyntax.CStyle));
    }
}
