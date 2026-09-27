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
/// Demonstrates BinaryReaderExtensions and BinaryWriterExtensions usage.
/// </summary>
internal sealed class BinaryReaderWriterSample : ISample
{
    /// <inheritdoc />
    public string Name => "io-binary";

    /// <inheritdoc />
    public string Description => "Writes and reads structs, strings, and aligned offsets with binary IO helpers.";

    /// <inheritdoc />
    public int Run(string[] arguments)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true);
        using BinaryReader reader = new(stream, Encoding.UTF8, leaveOpen: true);

        writer.WriteNullTerminatedString("fox");
        writer.WriteStruct(new BinaryReaderWriterEntry { Id = 7, Score = 42 });
        writer.WriteStructArray([new BinaryReaderWriterEntry { Id = 8, Score = 43 }]);
        stream.Position = 0;

        string text = reader.ReadUTF8NullTerminatedString();
        BinaryReaderWriterEntry first = reader.ReadStruct<BinaryReaderWriterEntry>();
        BinaryReaderWriterEntry second = reader.ReadStruct<BinaryReaderWriterEntry>();

        stream.Position = 5;
        long aligned = reader.Align(4);

        AnsiConsole.WriteLine($"String: {text}");
        AnsiConsole.WriteLine($"Entry1: Id={first.Id}, Score={first.Score}");
        AnsiConsole.WriteLine($"Entry2: Id={second.Id}, Score={second.Score}");
        AnsiConsole.WriteLine($"Aligned stream position: {aligned}");
        return 0;
    }
}
