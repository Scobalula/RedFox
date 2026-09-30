using RedFox.GameExtraction.Template;
using RedFox.GameExtraction.UI;

namespace RedFox.GameExtraction.Template.Avalonia;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        GameExtractionApp.Run(new GameExtractionConfig
        {
            AssetManagerFactory = TemplateAssetManagerFactory.Create,
            WindowTitle = "RedFox ZIP Explorer",
            SidebarTitle = "ZIP Explorer",
            Description = "Open ZIP archives and export raw entries.",
            AppName = "RedFoxZipExplorer",
            Version = "1.0.0",
            AccentColor = "#0B7D92",
            FileFilter = "All Files|*.*",
            SupportsFileSources = true,
            SupportsDirectorySources = false,
            SupportsProcessSources = true,
            MetadataColumns = ["CompressedSize", "ArchivePath"],
            ExportConfigurationFactory = TemplateSettings.CreateExportConfiguration,
            Settings = TemplateSettings.CreateDefaults(),
            SettingDefinitions = TemplateSettings.Definitions,
            About = new AboutConfig
            {
                Description = "A minimal Avalonia shell for ZIP-backed RedFox.GameExtraction sources.",
            },
        });
    }
}
