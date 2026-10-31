using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Nodes;
using RedFox.AppHost.Packer;

if (args.Length == 0 || args.Length % 2 != 0)
{
    Console.Error.WriteLine("""
        Usage: --publish <dir> --entry <App.dll> [--name <name>] [--version <version>] [--mode packed|splash] [--quality <0-11>] [--output <exe>] [--stub <exe>]
        Splash: [--splash <png>] [--font <family>] [--background <#RRGGBB>] [--foreground <#RRGGBB>] [--secondary <#RRGGBB>]
                [--accent <#RRGGBB>] [--border <#RRGGBB|#00000000>] [--corner-radius <pixels>]
        """);
    return 1;
}

var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

for (int i = 0; i < args.Length; i += 2)
{
    options[args[i].TrimStart('-')] = args[i + 1];
}

string publishDirectory	= Path.GetFullPath(options["publish"]);
string entry			= options["entry"];
string entryPath		= Path.Combine(publishDirectory, entry);
string sdkHostPath		= Path.ChangeExtension(entryPath, ".exe");
string name				= options.GetValueOrDefault("name", Path.GetFileNameWithoutExtension(entry));
string stubPath			= options.GetValueOrDefault("stub", Path.Combine(AppContext.BaseDirectory, "RedFoxAppHost.exe"));
AppHostMode mode		= options.GetValueOrDefault("mode", "packed") == "splash" ? AppHostMode.SplashOnly : AppHostMode.Packed;

string runtimeConfigPath = Path.ChangeExtension(entryPath, ".runtimeconfig.json");

if (!File.Exists(entryPath) || !File.Exists(runtimeConfigPath))
{
    Console.Error.WriteLine($"{entryPath} or its runtimeconfig.json is missing. Publish a framework-dependent or self-contained app; PublishAot and PublishSingleFile outputs are not supported.");
    return 1;
}

if (!File.Exists(sdkHostPath))
{
    Console.Error.WriteLine($"{sdkHostPath} is missing. Publish with UseAppHost enabled so its icon, version and manifest can be copied.");
    return 1;
}

string version		= options.GetValueOrDefault("version", FileVersionInfo.GetVersionInfo(entryPath).ProductVersion?.Split('+')[0] ?? string.Empty);
byte[] splash		= options.TryGetValue("splash", out string? splashPath) ? File.ReadAllBytes(splashPath) : [];
string outputPath	= Path.GetFullPath(options.GetValueOrDefault("output", mode == AppHostMode.SplashOnly ? sdkHostPath : Path.Combine(publishDirectory, "..", "packed", name + ".exe")));
string partialPath	= outputPath + ".partial";
int quality			= int.Parse(options.GetValueOrDefault("quality", "5"), CultureInfo.InvariantCulture);

JsonNode? runtimeOptions	= JsonNode.Parse(File.ReadAllText(runtimeConfigPath))?["runtimeOptions"];
JsonNode? framework			= runtimeOptions?["framework"] ?? runtimeOptions?["frameworks"]?.AsArray().LastOrDefault();
string frameworkName		= framework?["name"]?.GetValue<string>() ?? string.Empty;
string frameworkVersion		= framework?["version"]?.GetValue<string>() ?? string.Empty;

var style = new SplashStyle
{
    FontFamily		= options.GetValueOrDefault("font", "Segoe UI"),
    Background		= ReadColor(options, "background", 0xFF1E1E22),
    Foreground		= ReadColor(options, "foreground", 0xFFF0F0F0),
    SecondaryText	= ReadColor(options, "secondary", 0xFFA0A0A8),
    Accent			= ReadColor(options, "accent", 0xFFE8682A),
    Border			= ReadColor(options, "border", 0xFF3C3C42),
    CornerRadius	= uint.Parse(options.GetValueOrDefault("corner-radius", "0"), CultureInfo.InvariantCulture),
};

new FileInfo(outputPath).Directory?.Create();
File.Copy(stubPath, partialPath, true);
ExecutableResources.Copy(sdkHostPath, partialPath);
bool console = ExecutableResources.CopySubsystem(sdkHostPath, partialPath);

long compressed;
using (var output = File.Open(partialPath, FileMode.Open, FileAccess.ReadWrite))
{
    output.Seek(0, SeekOrigin.End);
    compressed = PayloadWriter.Write(output, mode, name, version, entry, frameworkName, frameworkVersion, splash, style, publishDirectory, sdkHostPath, quality);
}

try
{
    File.Move(partialPath, outputPath, true);
}
catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
{
    File.Delete(partialPath);
    Console.Error.WriteLine($"{outputPath} could not be replaced; close it if it is running. {exception.Message}");
    return 1;
}

Console.WriteLine($"{outputPath} ({(console ? "console" : "gui")}, {mode}, {new FileInfo(outputPath).Length:N0} bytes, payload {compressed:N0} bytes)");
return 0;

static uint ReadColor(Dictionary<string, string> options, string key, uint fallback)
{
    if (!options.TryGetValue(key, out string? value))
    {
        return fallback;
    }

    string hex = value.TrimStart('#');
    uint color = uint.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    return hex.Length == 6 ? color | 0xFF000000 : color;
}
