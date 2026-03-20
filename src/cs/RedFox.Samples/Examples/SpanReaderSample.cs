// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using System.Text;
using RedFox.IO;

namespace RedFox.Samples.Examples;

/// <summary>
/// Demonstrates reading binary data with SpanReader.
/// </summary>
internal sealed class SpanReaderSample : ISample
{
    /// <inheritdoc />
    public string Name => "io-span-reader";

    /// <inheritdoc />
    public string Description => "Reads structured values and strings from a byte span.";

    /// <inheritdoc />
    public int Run(string[] arguments)
    {
        byte[] buffer = new byte[32];
        BitConverter.GetBytes(0x11223344).CopyTo(buffer, 0);
        Encoding.UTF8.GetBytes("fox").CopyTo(buffer, 4);

        SpanReader spanReader = new(buffer);
        int number = spanReader.Read<int>();
        string text = spanReader.ReadString(3);
        long seekPosition = spanReader.Seek(2, SeekOrigin.Current);

        Console.WriteLine($"Number: 0x{number:X8}");
        Console.WriteLine($"Text: {text}");
        Console.WriteLine($"Position after seek: {seekPosition}");
        return 0;
    }
}
