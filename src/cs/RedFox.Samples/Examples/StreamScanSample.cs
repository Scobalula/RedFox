// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using RedFox.IO;

namespace RedFox.Samples.Examples;

/// <summary>
/// Demonstrates stream pattern scanning with wildcards.
/// </summary>
internal sealed class StreamScanSample : ISample
{
    /// <inheritdoc />
    public string Name => "io-stream-scan";

    /// <inheritdoc />
    public string Description => "Scans an in-memory stream for a wildcard pattern and prints all match offsets.";

    /// <inheritdoc />
    public int Run(string[] arguments)
    {
        byte[] data = [0x10, 0x48, 0x8B, 0x01, 0xFF, 0x48, 0x8B, 0x02, 0xAA];
        string patternText = arguments.Length > 0 ? string.Join(" ", arguments) : "48 8B ??";
        using MemoryStream stream = new(data, writable: false);

        long[] offsets = stream.Scan(patternText, 0, stream.Length, firstOccurence: false);
        Console.WriteLine($"Pattern text: {patternText}");
        Console.WriteLine($"Match count : {offsets.Length}");
        Console.WriteLine($"Offsets     : {string.Join(", ", offsets)}");
        return 0;
    }
}
