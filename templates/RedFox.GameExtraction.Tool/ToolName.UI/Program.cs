using RedFox.GameExtraction;
using RedFox.GameExtraction.UI;
using RedFox.GameExtraction.UI.ViewModels;
using ToolName;

namespace ToolName.UI;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        GameExtractionConfig config = new()
        {
            AssetManagerFactory = ToolNameAssetManagerFactory.Create,
            WindowTitle = "DISPLAY_NAME",
            SidebarTitle = "DISPLAY_NAME",
            Description = "DESCRIPTION_TEXT",
            AppName = "ToolName",
            IconPath = Path.Combine(AppContext.BaseDirectory, "Icon.png"),
            SidebarIconPath = Path.Combine(AppContext.BaseDirectory, "Icon.png"),
            Version = ToolNameBuildInfo.DisplayVersion,
            AccentColor = "#037599",
            FileFilter = "ZIP Archives|*.zip|All Files|*.*",
            SupportsFileSources = true,
            MetadataColumns = ["ArchivePath"],
            Settings = ToolNameSettings.CreateDefaults(),
            SettingDefinitions = [.. ToolNameSettings.Definitions, .. PreviewSettings.SettingDefinitions],
            About = new AboutConfig
            {
                Description = "DESCRIPTION_TEXT",
                Links =
                [
                    new AboutLink { Label = "Github", Url = "https://github.com/" },
                    new AboutLink { Label = "Donate", Url = "https://ko-fi.com/" },
                ],
            },
            Donation = new DonationConfig
            {
                Url = "https://ko-fi.com/",
                Message = "ToolName is free and always will be. If it saved you some time, consider buying me a coffee!",
            },
        };

        GameExtractionApp.Run(config, args);
    }
}
