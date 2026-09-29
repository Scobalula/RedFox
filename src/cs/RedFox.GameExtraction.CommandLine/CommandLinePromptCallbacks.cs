using PrettyPrompt;
using PrettyPrompt.Completion;
using PrettyPrompt.Consoles;
using PrettyPrompt.Documents;
using PrettyPrompt.Highlighting;

namespace RedFox.GameExtraction.CommandLine;

internal sealed class CommandLinePromptCallbacks(CommandLineSession session) : PromptCallbacks
{
    private const int MaximumCompletionItems = 200;
    private const int DescriptionSpacing = 3;

    private readonly AnsiColor _accentColor = PromptTheme.ToAnsiColor(session.Theme.Accent);
    private readonly AnsiColor _mutedColor = PromptTheme.ToAnsiColor(session.Theme.Muted);
    private readonly AnsiColor _errorColor = PromptTheme.ToAnsiColor(session.Theme.Error);

    protected override Task<TextSpan> GetSpanToReplaceByCompletionAsync(string text, int caret, CancellationToken cancellationToken)
    {
        (int start, int end) = CommandLineTokenizer.GetTokenBounds(text, caret);
        return Task.FromResult(TextSpan.FromBounds(start, end));
    }

    protected override Task<bool> ShouldOpenCompletionWindowAsync(string text, int caret, KeyPress keyPress, CancellationToken cancellationToken)
    {
        char character = keyPress.ConsoleKeyInfo.KeyChar;

        if ((character == '/' && caret <= 1) || (text.StartsWith('/') && character is ' ' or '/' or '\\'))
        {
            return Task.FromResult(true);
        }

        return base.ShouldOpenCompletionWindowAsync(text, caret, keyPress, cancellationToken);
    }

    protected override Task<IReadOnlyList<CompletionItem>> GetCompletionItemsAsync(string text, int caret, TextSpan spanToBeReplaced, CancellationToken cancellationToken)
    {
        string partial = text.Substring(spanToBeReplaced.Start, spanToBeReplaced.Length);
        IReadOnlyList<string> previous = CommandLineTokenizer.Tokenize(text[..spanToBeReplaced.Start]);
        IReadOnlyList<CompletionItem> items = previous.Count == 0 ? GetCommandItems(partial) : GetArgumentItems(previous, partial);

        return Task.FromResult(items);
    }

    protected override Task<IReadOnlyCollection<FormatSpan>> HighlightCallbackAsync(string text, CancellationToken cancellationToken)
    {
        if (!text.StartsWith('/'))
        {
            return Task.FromResult<IReadOnlyCollection<FormatSpan>>([]);
        }

        int length = text.IndexOf(' ') is int space and >= 0 ? space : text.Length;
        AnsiColor color = session.FindCommand(text[1..length]) is null ? _errorColor : _accentColor;

        return Task.FromResult<IReadOnlyCollection<FormatSpan>>([new FormatSpan(0, length, new ConsoleFormat(Foreground: color, Bold: true))]);
    }

    private IReadOnlyList<CompletionItem> GetCommandItems(string partial)
    {
        if (!partial.StartsWith('/'))
        {
            return [];
        }

        int width = session.Commands.Max(command => command.Name.Length) + 1 + DescriptionSpacing;

        return [.. session.Commands.Select(command => new CompletionItem("/" + command.Name, CreateDisplayText(command, width)))];
    }

    private IReadOnlyList<CompletionItem> GetArgumentItems(IReadOnlyList<string> previous, string partial)
    {
        if (!previous[0].StartsWith('/') || session.FindCommand(previous[0][1..]) is not ICommandLineCommand command)
        {
            return [];
        }

        IEnumerable<string> candidates = command.GetCompletions(session, [.. previous.Skip(1), partial.Trim('"')]);

        return [.. candidates.Distinct(StringComparer.OrdinalIgnoreCase).Take(MaximumCompletionItems).Select(candidate => new CompletionItem(Quote(candidate)))];
    }

    private FormattedString CreateDisplayText(ICommandLineCommand command, int width)
    {
        string name = "/" + command.Name;
        string text = name.PadRight(width) + command.Description;

        return new FormattedString(text, new FormatSpan(0, name.Length, new ConsoleFormat(Foreground: _accentColor)), new FormatSpan(width, command.Description.Length, new ConsoleFormat(Foreground: _mutedColor)));
    }

    private static string Quote(string candidate) => candidate.Contains(' ') && !candidate.StartsWith('"') ? $"\"{candidate}\"" : candidate;
}
