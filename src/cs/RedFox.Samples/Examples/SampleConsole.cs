namespace RedFox.Samples.Examples;

internal static class SampleConsole
{
    public static IAnsiConsole Error { get; } = AnsiConsole.Create(new AnsiConsoleSettings
    {
        Out = new AnsiConsoleOutput(Console.Error),
    });

    public static Style ErrorStyle { get; } = Style.Parse("red");
}
