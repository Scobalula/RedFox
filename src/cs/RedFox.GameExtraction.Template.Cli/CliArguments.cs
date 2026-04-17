namespace RedFox.GameExtraction.Template.Cli;

internal sealed class CliArguments
{
    public CliCommand Command { get; }

    public string? ZipPath { get; }

    public string? AssetPath { get; }

    public string? OutputDirectory { get; }

    private CliArguments(CliCommand command, string? zipPath = null, string? assetPath = null, string? outputDirectory = null)
    {
        Command = command;
        ZipPath = zipPath;
        AssetPath = assetPath;
        OutputDirectory = outputDirectory;
    }

    public static bool TryParse(string[] arguments, out CliArguments? parsedArguments, out string? error)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        parsedArguments = null;
        error = null;

        if (arguments.Length == 0)
        {
            parsedArguments = new CliArguments(CliCommand.Help);
            return true;
        }

        string command = arguments[0];
        if (IsHelpCommand(command))
        {
            parsedArguments = new CliArguments(CliCommand.Help);
            return true;
        }

        if (string.Equals(command, "list", StringComparison.OrdinalIgnoreCase))
        {
            return TryCreateList(arguments, out parsedArguments, out error);
        }

        if (string.Equals(command, "read", StringComparison.OrdinalIgnoreCase))
        {
            return TryCreateRead(arguments, out parsedArguments, out error);
        }

        if (string.Equals(command, "export", StringComparison.OrdinalIgnoreCase))
        {
            return TryCreateExport(arguments, out parsedArguments, out error);
        }

        if (string.Equals(command, "vfs", StringComparison.OrdinalIgnoreCase))
        {
            return TryCreateVfs(arguments, out parsedArguments, out error);
        }

        error = $"Unknown command '{command}'.";
        return false;
    }

    private static bool TryCreateList(string[] arguments, out CliArguments? parsedArguments, out string? error)
    {
        return TryRequireZipPath(arguments, CliCommand.List, out parsedArguments, out error);
    }

    private static bool TryCreateVfs(string[] arguments, out CliArguments? parsedArguments, out string? error)
    {
        return TryRequireZipPath(arguments, CliCommand.Vfs, out parsedArguments, out error);
    }

    private static bool TryCreateRead(string[] arguments, out CliArguments? parsedArguments, out string? error)
    {
        parsedArguments = null;
        error = null;

        if (arguments.Length < 3)
        {
            error = "Usage: read <zip-path> <asset-path>";
            return false;
        }

        parsedArguments = new CliArguments(CliCommand.Read, arguments[1], arguments[2]);
        return true;
    }

    private static bool TryCreateExport(string[] arguments, out CliArguments? parsedArguments, out string? error)
    {
        parsedArguments = null;
        error = null;

        if (arguments.Length < 3)
        {
            error = "Usage: export <zip-path> <output-directory>";
            return false;
        }

        parsedArguments = new CliArguments(CliCommand.Export, arguments[1], outputDirectory: arguments[2]);
        return true;
    }

    private static bool TryRequireZipPath(string[] arguments, CliCommand command, out CliArguments? parsedArguments, out string? error)
    {
        parsedArguments = null;
        error = null;

        if (arguments.Length < 2)
        {
            error = $"Usage: {command.ToString().ToLowerInvariant()} <zip-path>";
            return false;
        }

        parsedArguments = new CliArguments(command, arguments[1]);
        return true;
    }

    private static bool IsHelpCommand(string command)
    {
        return string.Equals(command, "help", StringComparison.OrdinalIgnoreCase)
            || string.Equals(command, "--help", StringComparison.OrdinalIgnoreCase)
            || string.Equals(command, "-h", StringComparison.OrdinalIgnoreCase);
    }
}
