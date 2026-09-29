using PrettyPrompt;
using PrettyPrompt.Highlighting;
using Spectre.Console;

namespace RedFox.GameExtraction.CommandLine;

internal static class PromptTheme
{
    private const string PromptSymbol = "❯ ";
    private const int MaximumCompletionItems = 12;

    public static PromptConfiguration CreateConfiguration(CommandLineTheme theme)
    {
        FormattedString prompt = new(PromptSymbol, new ConsoleFormat(Foreground: ToAnsiColor(theme.Accent), Bold: true));

        return new PromptConfiguration(prompt: prompt, completionBoxBorderFormat: new ConsoleFormat(Foreground: ToAnsiColor(theme.Muted)), maxCompletionItemsCount: MaximumCompletionItems);
    }

    public static AnsiColor ToAnsiColor(string color)
    {
        Color parsed = Style.Parse(color).Foreground;
        return AnsiColor.Rgb(parsed.R, parsed.G, parsed.B);
    }
}
