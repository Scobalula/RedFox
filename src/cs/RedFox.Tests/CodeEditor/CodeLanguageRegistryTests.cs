using RedFox.Avalonia.CodeEditor;
using TextMateSharp.Grammars;
using TextMateSharp.Registry;

namespace RedFox.Tests.CodeEditor;

public sealed class CodeLanguageRegistryTests
{
    [Theory]
    [InlineData("scripts/zm/_zm.gsc", "source.gsc")]
    [InlineData("scripts/zm/_zm.csc", "source.gsc")]
    [InlineData("scripts/shared/shared.gsh", "source.gsc")]
    [InlineData("gamedata/table.csv", "text.csv")]
    [InlineData("ui/hud.lua", "source.lua")]
    [InlineData("weapon.json", "source.json")]
    public void FindsTheGrammarForAssetFiles(string fileName, string scope)
    {
        Assert.Equal(scope, new CodeLanguageRegistry().FindScope(fileName));
    }

    [Theory]
    [InlineData("noextension")]
    [InlineData("data.unknownextension")]
    public void UnknownFilesHaveNoGrammar(string fileName)
    {
        Assert.Null(new CodeLanguageRegistry().FindScope(fileName));
    }

    [Theory]
    [InlineData("return true;", "return", "keyword.control.gsc")]
    [InlineData("return true;", "true", "constant.language.gsc")]
    [InlineData("function private foo( a )", "function", "storage.type.function.gsc")]
    [InlineData("function private foo( a )", "private", "storage.modifier.gsc")]
    [InlineData("function private foo( a )", "foo", "entity.name.function.gsc")]
    [InlineData("function private foo( a )", "a )", "variable.parameter.gsc")]
    [InlineData("#using scripts\\shared\\util_shared;", "#using", "keyword.control.directive.gsc")]
    [InlineData("#using scripts\\shared\\util_shared;", "scripts\\shared\\util_shared", "string.unquoted.path.gsc")]
    [InlineData("x = GHOST_RANGE_SQ;", "GHOST_RANGE_SQ", "variable.other.constant.gsc")]
    [InlineData("x = IS_TRUE( y );", "IS_TRUE", "entity.name.function.gsc")]
    [InlineData("level thread foo::bar();", "bar", "entity.name.function.gsc")]
    [InlineData("level thread foo::bar();", "thread", "storage.type.gsc")]
    [InlineData("self.x = 1.5;", "self", "variable.language.gsc")]
    [InlineData("self.x = 1.5;", "1.5", "constant.numeric.gsc")]
    [InlineData("x = \"text\"; // note", "// note", "comment.line.double-slash.gsc")]
    public void GscTokensTakeTheirScopes(string line, string text, string scope)
    {
        Assert.Contains(scope, GetScopes("source.gsc", line, text));
    }

    [Theory]
    [InlineData("name,12,true", "12", "constant.numeric.csv")]
    [InlineData("name,12,true", "true", "constant.language.csv")]
    [InlineData("12,name", "12", "constant.numeric.csv")]
    [InlineData("a,\"b, c\",d", "\"b, c\"", "string.quoted.double.csv")]
    public void CsvTokensTakeTheirScopes(string line, string text, string scope)
    {
        Assert.Contains(scope, GetScopes("text.csv", line, text));
    }

    private static List<string> GetScopes(string grammarScope, string line, string text)
    {
        Registry registry = new(new CodeLanguageRegistry());
        IGrammar grammar = registry.LoadGrammar(grammarScope);
        ITokenizeLineResult result = grammar.TokenizeLine(line);
        int start = line.IndexOf(text, StringComparison.Ordinal);

        return [.. result.Tokens.Where(token => token.StartIndex >= start && token.StartIndex < start + text.Length).SelectMany(token => token.Scopes)];
    }
}
