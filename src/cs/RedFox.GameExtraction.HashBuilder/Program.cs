using System.Security.Cryptography;
using System.Text;
using RedFox.GameExtraction.Hashing;
using Spectre.Console;

IAnsiConsole errorConsole = AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(Console.Error) });
Style errorStyle = Style.Parse("red");

if (args.Length < 1)
{
    errorConsole.WriteLine("Usage: HashBuilder <input> [output] [--algorithm <name>] [--compress] [--metadata key=value]...");
    errorConsole.WriteLine();
    errorConsole.WriteLine("  Formats: .namefile, .csv, .txt (auto-detected by extension)");
    errorConsole.WriteLine("  --algorithm   Required for .txt input files");
    errorConsole.WriteLine("  --compress    Compress .namefile output (FastLZ)");
    errorConsole.WriteLine("  --metadata    Add arbitrary metadata (repeatable, key=value format)");
    errorConsole.WriteLine();
    errorConsole.WriteLine("  Metadata auto-tagged on save: Timestamp, SourceFile");
    errorConsole.WriteLine();
    errorConsole.WriteLine("  Examples:");
    errorConsole.WriteLine("    HashBuilder names.txt names.namefile --algorithm Fnv1a64");
    errorConsole.WriteLine("    HashBuilder hashes.csv hashes.namefile --compress");
    errorConsole.WriteLine("    HashBuilder data.namefile dump.csv");
    errorConsole.WriteLine("    HashBuilder names.txt out.namefile --algorithm Fnv1a64 --metadata Game=Bo2 --metadata Author=Me");
    return 1;
}

var inputPath = args[0];
var outputPath = args.Length > 1 && !args[1].StartsWith("--") ? args[1] : Path.ChangeExtension(inputPath, ".namefile");

string? hashAlgorithm = null;
var compress = false;
var checksum = true;
var metadataPairs = new List<string>();

for (var i = 1; i < args.Length; i++)
{
    if ((args[i] is "--algorithm" or "--name") && i + 1 < args.Length)
        hashAlgorithm = args[++i];
    else if (args[i] is "--compress")
        compress = true;
    else if (args[i] is "--nochecksum")
        checksum = false;
    else if (args[i] is "--metadata" && i + 1 < args.Length)
        metadataPairs.Add(args[++i]);
}

if (!File.Exists(inputPath))
{
    errorConsole.WriteLine($"Input file not found: {inputPath}", errorStyle);
    return 1;
}

var inputExt = Path.GetExtension(inputPath).ToLowerInvariant();

AnsiConsole.WriteLine($"Loading: {inputPath}");

NameTable nameTable;

if (inputExt == ".namefile")
{
    nameTable = NameFile.Load(inputPath);
}
else if (inputExt == ".csv")
{
    nameTable = hashAlgorithm is not null ? NameFile.Load(inputPath, hashAlgorithm) : NameFile.Load(inputPath);
}
else if (inputExt == ".txt")
{
    if (hashAlgorithm is null)
    {
        errorConsole.WriteLine("Error: --algorithm is required for .txt input files.", errorStyle);
        return 1;
    }

    AnsiConsole.WriteLine($"Hashing with: SHA256 (tagged as '{hashAlgorithm}')");

    nameTable = NameFile.Load(inputPath, hashAlgorithm, name =>
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(name.ToString()));
        return new NameKey(bytes);
    });
}
else
{
    errorConsole.WriteLine($"Unsupported input format: '{inputExt}'", errorStyle);
    return 1;
}

AnsiConsole.WriteLine($"Algorithm: {nameTable.Name}");
AnsiConsole.WriteLine($"Entries:   {nameTable.Count:N0}");

nameTable.Metadata["Timestamp"] = DateTime.UtcNow.ToString("o");
nameTable.Metadata["SourceFile"] = Path.GetFileName(inputPath);

foreach (var pair in metadataPairs)
{
    var eq = pair.IndexOf('=');

    if (eq > 0)
    {
        nameTable.Metadata[pair[..eq].Trim()] = pair[(eq + 1)..].Trim();
    }
    else
    {
        errorConsole.WriteLine($"Warning: Skipping malformed --metadata value '{pair}' (expected key=value).", errorStyle);
    }
}

if (nameTable.Metadata.Count > 0)
{
    AnsiConsole.WriteLine($"Metadata:  {nameTable.Metadata.Count} key(s)");
}

AnsiConsole.WriteLine($"Saving:    {outputPath}");

var flags = NameFileFlags.None;

if (compress)
    flags |= NameFileFlags.Compressed;
if (checksum)
    flags |= NameFileFlags.Checksum;

NameFile.Save(outputPath, nameTable, flags);

AnsiConsole.MarkupLine("[green]Done.[/]");
return 0;
