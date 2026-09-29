using PrettyPrompt;
using Spectre.Console;

namespace RedFox.GameExtraction.CommandLine;

internal sealed class CommandLineShell(CommandLineSession session)
{
    private const int SuccessExitCode = 0;
    private const int FailureExitCode = 1;
    private const int CancelledExitCode = 130;
    private const string DefaultCommandName = "list";

    private CancellationTokenSource? _commandCancellation;

    public async Task<int> RunAsync(string[] args)
    {
        IReadOnlyList<IReadOnlyList<string>> invocations = CommandLineTokenizer.GroupArguments(args, name => session.FindCommand(name) is not null);
        bool isScripted = Console.IsInputRedirected || invocations.Any(invocation => !invocation[0].Equals(CommandLineTokenizer.MountCommandName, StringComparison.OrdinalIgnoreCase));

        session.IsInteractive = !isScripted;
        Console.CancelKeyPress += OnCancelKeyPress;

        try
        {
            if (session.IsInteractive)
            {
                WelcomeView.Render(session);
            }

            if (session.Config.Startup is not null)
            {
                int startupExitCode = await RunCommandAsync(cancellationToken => session.Config.Startup(session, cancellationToken)).ConfigureAwait(false);

                if (startupExitCode != SuccessExitCode)
                {
                    return startupExitCode;
                }
            }

            foreach (IReadOnlyList<string> invocation in invocations)
            {
                int exitCode = await ExecuteAsync(invocation).ConfigureAwait(false);

                if (isScripted && exitCode != SuccessExitCode)
                {
                    return exitCode;
                }
            }

            return isScripted ? SuccessExitCode : await RunPromptAsync().ConfigureAwait(false);
        }
        finally
        {
            Console.CancelKeyPress -= OnCancelKeyPress;
        }
    }

    private async Task<int> RunPromptAsync()
    {
        string historyPath = Path.Combine(Path.GetDirectoryName(session.SettingsPath)!, "history.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(historyPath)!);

        await using Prompt prompt = new(historyPath, new CommandLinePromptCallbacks(session), null, PromptTheme.CreateConfiguration(session.Theme));
        bool isExitPending = false;

        while (!session.IsExitRequested)
        {
            PromptResult result = await prompt.ReadLineAsync().ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                if (isExitPending)
                {
                    break;
                }

                isExitPending = true;
                session.WriteMuted("Press Ctrl+C again to exit");
                continue;
            }

            isExitPending = false;
            IReadOnlyList<string> tokens = CommandLineTokenizer.Tokenize(result.Text);

            if (tokens.Count == 0)
            {
                continue;
            }

            await ExecuteAsync(tokens[0].StartsWith('/') ? [tokens[0][1..], .. tokens.Skip(1)] : [DefaultCommandName, .. tokens]).ConfigureAwait(false);
            session.Console.WriteLine();
        }

        return SuccessExitCode;
    }

    private Task<int> ExecuteAsync(IReadOnlyList<string> invocation)
    {
        ICommandLineCommand? command = session.FindCommand(invocation[0]);

        if (command is null)
        {
            session.WriteError($"Unknown command /{Markup.Escape(invocation[0])}");
            return Task.FromResult(FailureExitCode);
        }

        IReadOnlyList<string> arguments = [.. invocation.Skip(1)];

        return RunCommandAsync(cancellationToken => command.ExecuteAsync(session, arguments, cancellationToken));
    }

    private async Task<int> RunCommandAsync(Func<CancellationToken, Task> command)
    {
        CancellationTokenSource cancellation = new();
        _commandCancellation = cancellation;

        try
        {
            await command(cancellation.Token).ConfigureAwait(false);
            return SuccessExitCode;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            session.WriteWarning("Cancelled");
            return CancelledExitCode;
        }
        catch (Exception exception)
        {
            session.WriteError(Markup.Escape(exception.Message));
            return FailureExitCode;
        }
        finally
        {
            _commandCancellation = null;
        }
    }

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs eventArgs)
    {
        CancellationTokenSource? cancellation = _commandCancellation;

        if (cancellation is null || cancellation.IsCancellationRequested)
        {
            return;
        }

        eventArgs.Cancel = true;
        cancellation.Cancel();
    }
}
