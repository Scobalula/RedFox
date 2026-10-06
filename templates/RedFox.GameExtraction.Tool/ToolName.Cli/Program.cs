using RedFox.GameExtraction;
using RedFox.GameExtraction.CommandLine;
using ToolName;

namespace ToolName.Cli;

internal static class Program
{
    private static Task<int> Main(string[] args) => GameExtractionCommandLineApp.RunAsync(new GameExtractionCommandLineConfig
    {
        AssetManagerFactory = ToolNameAssetManagerFactory.Create,
        Settings = ToolNameSettings.CreateDefaults(),
        SettingDefinitions = ToolNameSettings.Definitions,
        Title = "DISPLAY_NAME",
        Description = "DESCRIPTION_TEXT",
        AppName = "ToolName",
        Version = ToolNameBuildInfo.DisplayVersion,
        Theme = new CommandLineTheme
        {
            Accent = "#4AC7EE",
        },
        Donation = new DonationConfig
        {
            Url = "https://ko-fi.com/",
            Message = "ToolName is free and always will be. If it saved you some time, consider buying me a coffee!",
        },
    }, args);
}
