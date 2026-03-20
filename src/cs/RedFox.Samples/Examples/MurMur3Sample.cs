// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using System.Text;
using RedFox.Cryptography.MurMur3;

namespace RedFox.Samples.Examples;

/// <summary>
/// Demonstrates MurMur3 hashing helpers.
/// </summary>
internal sealed class MurMur3Sample : ISample
{
    /// <inheritdoc />
    public string Name => "crypto-murmur3";

    /// <inheritdoc />
    public string Description => "Computes MurMur3 values using static helpers and incremental hash API.";

    /// <inheritdoc />
    public int Run(string[] arguments)
    {
        string input = arguments.Length > 0 ? string.Join(" ", arguments) : "RedFox";
        uint seed = 0x12345678;
        uint staticHash = MurMur3.Calculate32UTF8(input, seed);

        using MurMur3Hash hash = new(seed);
        byte[] incrementalBytes = hash.ComputeHash(Encoding.UTF8.GetBytes(input));
        uint incrementalHash = BitConverter.ToUInt32(incrementalBytes, 0);

        Console.WriteLine($"Input: {input}");
        Console.WriteLine($"Static hash     : 0x{staticHash:X8}");
        Console.WriteLine($"Incremental hash: 0x{incrementalHash:X8}");
        return 0;
    }
}
