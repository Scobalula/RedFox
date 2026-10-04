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
            Description = "Open ZIP archives to preview and export their entries.",
            AppName = "RedFoxZipExplorer",
            Version = "1.0.0",
            AccentColor = "#0e6996",
            FileFilter = "All Files|*.*",
            SupportsFileSources = true,
            SupportsDirectorySources = false,
            SupportsProcessSources = true,
            MetadataColumns = ["CompressedSize", "ArchivePath"],
            Settings = TemplateSettings.CreateDefaults(),
            SettingDefinitions = TemplateSettings.Definitions,
            About = new AboutConfig
            {
                Description = "A minimal Avalonia shell for ZIP-backed RedFox.GameExtraction sources.",
            },
            Donation = new DonationConfig
            {
                Url = "https://github.com/sponsors/Scobalula",
                Message = "ZIP Explorer is free and always will be. If it saved you some time, consider buying me a coffee!",
            },
        });
    }
}
