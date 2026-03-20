// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using RedFox;

namespace RedFox.Samples.Examples;

/// <summary>
/// Demonstrates parsing wildcard byte patterns.
/// </summary>
internal sealed class BytePatternParseSample : ISample
{
    /// <inheritdoc />
    public string Name => "pattern-parse";

    /// <inheritdoc />
    public string Description => "Parses a byte pattern like \"FF ?? AB\" and prints needle/mask values.";

    /// <inheritdoc />
    public int Run(string[] arguments)
    {
        string patternText = arguments.Length > 0 ? string.Join(" ", arguments) : "48 8B ?? ?? 89";
        Pattern<byte> pattern = BytePattern.Parse(patternText);

        Console.WriteLine($"Pattern text: {patternText}");
        Console.WriteLine($"Needle      : {FormatBytes(pattern.Needle)}");
        Console.WriteLine($"Mask        : {FormatBytes(pattern.Mask)}");
        Console.WriteLine("Note: mask bytes of FF indicate wildcard bytes in the pattern.");
        return 0;
    }

    private static string FormatBytes(byte[] bytes)
    {
        return string.Join(' ', bytes.Select(value => value.ToString("X2")));
    }
}
