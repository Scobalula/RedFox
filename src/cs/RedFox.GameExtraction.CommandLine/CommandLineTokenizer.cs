using System.Text;

namespace RedFox.GameExtraction.CommandLine;

internal static class CommandLineTokenizer
{
    public const string MountCommandName = "mount";

    public static IReadOnlyList<string> Tokenize(string line)
    {
        List<string> tokens = [];
        StringBuilder token = new();
        bool isQuoted = false;
        bool hasToken = false;

        foreach (char character in line)
        {
            if (character == '"')
            {
                isQuoted = !isQuoted;
                hasToken = true;
                continue;
            }

            if (char.IsWhiteSpace(character) && !isQuoted)
            {
                if (hasToken)
                {
                    tokens.Add(token.ToString());
                    token.Clear();
                    hasToken = false;
                }

                continue;
            }

            token.Append(character);
            hasToken = true;
        }

        if (hasToken)
        {
            tokens.Add(token.ToString());
        }

        return tokens;
    }

    public static (int Start, int End) GetTokenBounds(string line, int caret)
    {
        bool isQuoted = false;
        int start = 0;

        for (int index = 0; index < caret; index++)
        {
            if (line[index] == '"')
            {
                isQuoted = !isQuoted;
            }
            else if (!isQuoted && char.IsWhiteSpace(line[index]))
            {
                start = index + 1;
            }
        }

        int end = caret;

        while (end < line.Length && (isQuoted || !char.IsWhiteSpace(line[end])))
        {
            if (line[end] == '"')
            {
                isQuoted = !isQuoted;
            }

            end++;
        }

        return (start, end);
    }

    public static IReadOnlyList<IReadOnlyList<string>> GroupArguments(IReadOnlyList<string> arguments, Func<string, bool> isCommand)
    {
        List<IReadOnlyList<string>> invocations = [];
        List<string>? invocation = null;

        foreach (string argument in arguments)
        {
            if (argument.StartsWith('/') && isCommand(argument[1..]))
            {
                invocation = [argument[1..]];
                invocations.Add(invocation);
                continue;
            }

            if (invocation is null)
            {
                invocation = [MountCommandName];
                invocations.Add(invocation);
            }

            invocation.Add(argument);
        }

        return invocations;
    }
}
