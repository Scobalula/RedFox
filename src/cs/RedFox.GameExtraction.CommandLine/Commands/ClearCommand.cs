using Spectre.Console;

namespace RedFox.GameExtraction.CommandLine.Commands;

internal sealed class ClearCommand : ICommandLineCommand
{
    public string Name => "clear";

    public IReadOnlyList<string> Aliases => ["cls"];

    public string Description => "Clear the screen";

    public Task ExecuteAsync(CommandLineSession session, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        session.Console.Clear();
        WelcomeView.Render(session);

        return Task.CompletedTask;
    }
}
