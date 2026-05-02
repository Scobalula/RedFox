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
            FileFilter = "ZIP Archives|*.zip|All Files|*.*",
            SupportsFileSources = true,
            SupportsDirectorySources = false,
            SupportsProcessSources = false,
            MetadataColumns = ["CompressedSize", "ArchivePath"],
            Settings = new GameExtractionSettings
            {
                ExportReferences = false,
                PreserveDirectoryStructure = true,
            },
            About = new AboutConfig
            {
                Description = "A minimal Avalonia shell for ZIP-backed RedFox.GameExtraction sources.",
            },
        });
    }
}
