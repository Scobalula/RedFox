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

        Console.Error.WriteLine($"Unknown command '{command}'.");
        WriteUsage(samples);
        return 1;
    }

    private static int RunSample(IReadOnlyDictionary<string, ISample> samples, string[] arguments)
    {
        if (arguments.Length < 2)
        {
            Console.Error.WriteLine("Missing sample name. Use 'list' to see available samples.");
            return 1;
        }

        string sampleName = arguments[1];
        if (!samples.TryGetValue(sampleName, out ISample? sample))
        {
            Console.Error.WriteLine($"Unknown sample '{sampleName}'. Use 'list' to view available samples.");
            return 1;
        }

        string[] sampleArguments = arguments.Length > 2 ? arguments[2..] : [];
        try
        {
            return sample.Run(sampleArguments);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Sample '{sampleName}' failed: {exception.Message}");
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
        Console.WriteLine("RedFox samples CLI");
        Console.WriteLine("Usage:");
        Console.WriteLine("  RedFox.Samples list");
        Console.WriteLine("  RedFox.Samples run <sample-name> [sample arguments]");
        Console.WriteLine();
        WriteSampleList(samples);
    }

    private static void WriteSampleList(IReadOnlyDictionary<string, ISample> samples)
    {
        Console.WriteLine("Available samples:");
        IOrderedEnumerable<KeyValuePair<string, ISample>> orderedSamples = samples.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, ISample> sample in orderedSamples)
        {
            Console.WriteLine($"  {sample.Key,-22} {sample.Value.Description}");
        }
    }
}
