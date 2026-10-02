using System.Diagnostics;
using Spectre.Console;

namespace RedFox.GameExtraction.CommandLine.Commands;

internal sealed class OpenCommand : ICommandLineCommand
{
    public string Name => "open";

    public string Description => "Open the output folder";

    public Task ExecuteAsync(CommandLineSession session, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        string directory = Path.GetFullPath(session.CreateConfiguration().GetOption("OutputDirectory", GameExtractionSettings.GetDefaultOutputDirectory()));

        Directory.CreateDirectory(directory);
        Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true })?.Dispose();
        session.WriteSuccess($"Opened {Markup.Escape(directory)}");

        return Task.CompletedTask;
    }
}
