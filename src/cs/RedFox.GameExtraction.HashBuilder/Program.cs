using System.Security.Cryptography;
using System.Text;
using RedFox.GameExtraction.Hashing;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: HashBuilder <input> [output] [--algorithm <name>] [--compress] [--metadata key=value]...");
    Console.Error.WriteLine();
    Console.Error.WriteLine("  Formats: .namefile, .csv, .txt (auto-detected by extension)");
    Console.Error.WriteLine("  --algorithm   Required for .txt input files");
    Console.Error.WriteLine("  --compress    Compress .namefile output (FastLZ)");
    Console.Error.WriteLine("  --metadata    Add arbitrary metadata (repeatable, key=value format)");
    Console.Error.WriteLine();
    Console.Error.WriteLine("  Metadata auto-tagged on save: Timestamp, SourceFile");
    Console.Error.WriteLine();
    Console.Error.WriteLine("  Examples:");
    Console.Error.WriteLine("    HashBuilder names.txt names.namefile --algorithm Fnv1a64");
    Console.Error.WriteLine("    HashBuilder hashes.csv hashes.namefile --compress");
    Console.Error.WriteLine("    HashBuilder data.namefile dump.csv");
    Console.Error.WriteLine("    HashBuilder names.txt out.namefile --algorithm Fnv1a64 --metadata Game=Bo2 --metadata Author=Me");
    return 1;
}

var inputPath = args[0];
var outputPath = args.Length > 1 && !args[1].StartsWith("--") ? args[1] : Path.ChangeExtension(inputPath, ".namefile");

string? hashAlgorithm = null;
var compress = true;
var checksum = true;
var metadataPairs = new List<string>();

for (var i = 1; i < args.Length; i++)
{
    if (args[i] is "--name" && i + 1 < args.Length)
        hashAlgorithm = args[++i];
    else if (args[i] is "--nocompress")
        compress = false;
    else if (args[i] is "--nochecksum")
        checksum = false;
    else if (args[i] is "--metadata" && i + 1 < args.Length)
        metadataPairs.Add(args[++i]);
}

if (!File.Exists(inputPath))
{
    Console.Error.WriteLine($"Input file not found: {inputPath}");
    return 1;
}

var inputExt = Path.GetExtension(inputPath).ToLowerInvariant();

Console.WriteLine($"Loading: {inputPath}");

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
        Console.Error.WriteLine("Error: --algorithm is required for .txt input files.");
        return 1;
    }

    Console.WriteLine($"Hashing with: SHA256 (tagged as '{hashAlgorithm}')");

    nameTable = NameFile.Load(inputPath, hashAlgorithm, name =>
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(name.ToString()));
        return new NameKey(bytes);
    });
}
else
{
    Console.Error.WriteLine($"Unsupported input format: '{inputExt}'");
    return 1;
}

Console.WriteLine($"Algorithm: {nameTable.Name}");
Console.WriteLine($"Entries:   {nameTable.Count:N0}");

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
        Console.Error.WriteLine($"Warning: Skipping malformed --metadata value '{pair}' (expected key=value).");
    }
}

if (nameTable.Metadata.Count > 0)
{
    Console.WriteLine($"Metadata:  {nameTable.Metadata.Count} key(s)");
}

Console.WriteLine($"Saving:    {outputPath}");

var flags = NameFileFlags.None;

if (compress)
    flags |= NameFileFlags.Compressed;
if (checksum)
    flags |= NameFileFlags.Checksum;

NameFile.Save(outputPath, nameTable, flags);

Console.WriteLine("Done.");
return 0;
