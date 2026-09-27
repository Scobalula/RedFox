// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using RedFox.Samples.Examples;

namespace RedFox.Samples;

internal static class Program
{
    [STAThread]
    private static int Main(string[] arguments)
    {
        IReadOnlyDictionary<string, ISample> samples = SampleRegistry.Create();
        if (arguments.Length == 0 || IsHelpCommand(arguments[0]))
        {
            WriteUsage(samples);
            return 0;
        }

        string command = arguments[0];
        if (string.Equals(command, "list", StringComparison.OrdinalIgnoreCase))
        {
            WriteSampleList(samples);
            return 0;
        }

        if (string.Equals(command, "run", StringComparison.OrdinalIgnoreCase))
        {
            return RunSample(samples, arguments);
        }

        SampleConsole.Error.WriteLine($"Unknown command '{command}'.", SampleConsole.ErrorStyle);
        WriteUsage(samples);
        return 1;
    }

    private static int RunSample(IReadOnlyDictionary<string, ISample> samples, string[] arguments)
    {
        if (arguments.Length < 2)
        {
            SampleConsole.Error.WriteLine("Missing sample name. Use 'list' to see available samples.", SampleConsole.ErrorStyle);
            return 1;
        }

        string sampleName = arguments[1];
        if (!samples.TryGetValue(sampleName, out ISample? sample))
        {
            SampleConsole.Error.WriteLine($"Unknown sample '{sampleName}'. Use 'list' to view available samples.", SampleConsole.ErrorStyle);
            return 1;
        }

        string[] sampleArguments = arguments.Length > 2 ? arguments[2..] : [];
        try
        {
            return sample.Run(sampleArguments);
        }
        catch (Exception exception)
        {
            SampleConsole.Error.WriteLine($"Sample '{sampleName}' failed: {exception}", SampleConsole.ErrorStyle);
            return 1;
        }
    }

    private static bool IsHelpCommand(string command)
    {
        return string.Equals(command, "help", StringComparison.OrdinalIgnoreCase)
            || string.Equals(command, "--help", StringComparison.OrdinalIgnoreCase)
            || string.Equals(command, "-h", StringComparison.OrdinalIgnoreCase);
    }

    private static void WriteUsage(IReadOnlyDictionary<string, ISample> samples)
    {
        AnsiConsole.WriteLine("RedFox samples CLI");
        AnsiConsole.WriteLine("Usage:");
        AnsiConsole.WriteLine("  RedFox.Samples list");
        AnsiConsole.WriteLine("  RedFox.Samples run <sample-name> [sample arguments]");
        AnsiConsole.WriteLine();
        WriteSampleList(samples);
    }

    private static void WriteSampleList(IReadOnlyDictionary<string, ISample> samples)
    {
        AnsiConsole.WriteLine("Available samples:");
        IOrderedEnumerable<KeyValuePair<string, ISample>> orderedSamples = samples.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, ISample> sample in orderedSamples)
        {
            AnsiConsole.WriteLine($"  {sample.Key,-22} {sample.Value.Description}");
        }
    }
}
