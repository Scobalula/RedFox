namespace RedFox.GameExtraction.CommandLine;

/// <summary>
/// Defines a slash command available in the command line shell.
/// </summary>
public interface ICommandLineCommand
{
    /// <summary>
    /// Gets the command name typed after the slash, such as <c>extract</c>.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets alternative names for the command.
    /// </summary>
    IReadOnlyList<string> Aliases => [];

    /// <summary>
    /// Gets the argument syntax shown in help, such as <c>[pattern] [type:&lt;type&gt;]</c>.
    /// </summary>
    string Usage => string.Empty;

    /// <summary>
    /// Gets the short description shown in help and completion.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Executes the command. Throw an exception to report a failure; its message is shown to the user.
    /// </summary>
    /// <param name="session">The current session.</param>
    /// <param name="arguments">The arguments following the command name.</param>
    /// <param name="cancellationToken">The token cancelled when the user presses Ctrl+C.</param>
    /// <returns>A task representing the command execution.</returns>
    Task ExecuteAsync(CommandLineSession session, IReadOnlyList<string> arguments, CancellationToken cancellationToken);

    /// <summary>
    /// Gets completion candidates for the argument currently being typed.
    /// </summary>
    /// <param name="session">The current session.</param>
    /// <param name="arguments">The arguments typed so far. The last element is the partial argument being completed.</param>
    /// <returns>The candidate values for the last argument.</returns>
    IEnumerable<string> GetCompletions(CommandLineSession session, IReadOnlyList<string> arguments) => [];
}
