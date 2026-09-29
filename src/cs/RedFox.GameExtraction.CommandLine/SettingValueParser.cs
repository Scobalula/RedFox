namespace RedFox.GameExtraction.CommandLine;

internal static class SettingValueParser
{
    private static readonly string[] TrueValues = ["true", "yes", "on", "1"];
    private static readonly string[] FalseValues = ["false", "no", "off", "0"];

    public static string Parse(GameExtractionSetting setting, string input) => setting.Type switch
    {
        GameExtractionSettingType.Boolean => ParseBoolean(setting, input),
        GameExtractionSettingType.Choice => ParseChoice(setting, input),
        GameExtractionSettingType.FilePath or GameExtractionSettingType.DirectoryPath => Path.GetFullPath(input),
        _ => input,
    };

    public static IEnumerable<string> GetCompletions(GameExtractionSetting setting, string partial) => setting.Type switch
    {
        GameExtractionSettingType.Boolean => [bool.TrueString.ToLowerInvariant(), bool.FalseString.ToLowerInvariant()],
        GameExtractionSettingType.Choice => setting.Options,
        GameExtractionSettingType.FilePath or GameExtractionSettingType.DirectoryPath => PathCompletion.GetCompletions(partial),
        _ => [],
    };

    private static string ParseBoolean(GameExtractionSetting setting, string input)
    {
        if (TrueValues.Contains(input, StringComparer.OrdinalIgnoreCase))
        {
            return bool.TrueString;
        }

        if (FalseValues.Contains(input, StringComparer.OrdinalIgnoreCase))
        {
            return bool.FalseString;
        }

        throw new ArgumentException($"{setting.Name} expects true or false");
    }

    private static string ParseChoice(GameExtractionSetting setting, string input)
    {
        string? option = setting.Options.FirstOrDefault(option => option.Equals(input, StringComparison.OrdinalIgnoreCase));

        return option ?? throw new ArgumentException($"{setting.Name} expects one of: {string.Join(", ", setting.Options)}");
    }
}
