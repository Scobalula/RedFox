using System.Diagnostics;
using Spectre.Console;

namespace RedFox.GameExtraction.CommandLine;

internal static class DonationView
{
    public const string HeartColor = "#E53935";

    private const string DonateChoice = "♥ Donate";

    private const string ContinueChoice = "Continue without donating";

    public static void Render(CommandLineSession session)
    {
        if (session.Config.Donation is not { } donation || !DonationConfig.TryMarkPrompted(session.Settings))
        {
            return;
        }

        IAnsiConsole console = session.Console;
        CommandLineTheme theme = session.Theme;
        string url = Markup.Escape(donation.Url);
        string message = string.IsNullOrWhiteSpace(donation.Message) ? string.Empty : $"{Markup.Escape(donation.Message)}\n\n";

        Panel panel = new Panel(new Markup($"{message}[{theme.Muted}]Donate:[/] [{theme.Accent} link={url}]{url}[/]"))
        {
            Header = new PanelHeader($" [{HeartColor}]♥[/] [bold]Support {Markup.Escape(session.Config.Title)}[/] "),
            Border = BoxBorder.Rounded,
            BorderStyle = Style.Parse(HeartColor),
            Padding = new Padding(2, 1),
        };

        console.Write(panel);
        console.WriteLine();

        SelectionPrompt<string> prompt = new SelectionPrompt<string>().Title($"[{HeartColor}]♥[/] Would you like to support {Markup.Escape(session.Config.Title)} with a donation? [{theme.Muted}](this won't be shown again)[/]").HighlightStyle(Style.Parse(theme.Accent)).AddChoices(DonateChoice, ContinueChoice);

        if (console.Prompt(prompt) == DonateChoice)
        {
            Process.Start(new ProcessStartInfo(donation.Url) { UseShellExecute = true })?.Dispose();
        }

        session.SaveSettings();
        console.WriteLine();
    }
}
