using RedFox.GameExtraction.CommandLine;

namespace RedFox.GameExtraction.Template.Cli;

internal static class Program
{
    private static Task<int> Main(string[] args) => GameExtractionCommandLineApp.RunAsync(new GameExtractionCommandLineConfig
    {
        AssetManagerFactory = TemplateAssetManagerFactory.Create,
        ExportConfigurationFactory = TemplateSettings.CreateExportConfiguration,
        Settings = TemplateSettings.CreateDefaults(),
        SettingDefinitions = TemplateSettings.Definitions,
        Title = "ZIP Explorer",
        Description = "Open ZIP archives and export raw entries.",
        AppName = "RedFoxZipExplorer",
        Version = "1.0.0",
        Author = "Scobalula",
        Theme = new CommandLineTheme
        {
            Accent = "#007000",
        },
        MetadataColumns = ["CompressedSize"],
    }, args);
}
