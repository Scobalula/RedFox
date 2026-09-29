namespace RedFox.GameExtraction.CommandLine.Commands;

internal sealed class ExitCommand : ICommandLineCommand
{
    public string Name => "exit";

    public IReadOnlyList<string> Aliases => ["quit", "q"];

    public string Description => "Exit the application";

    public Task ExecuteAsync(CommandLineSession session, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        session.RequestExit();

        return Task.CompletedTask;
    }
}
