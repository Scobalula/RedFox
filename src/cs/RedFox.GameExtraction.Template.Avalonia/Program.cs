using RedFox.GameExtraction.Template;
using RedFox.GameExtraction.UI;
using RedFox.GameExtraction.UI.Controls;
using RedFox.Graphics3D;

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
            AccentColor = "#007000",
            FileFilter = "ZIP Archives|*.zip|All Files|*.*",
            SupportsFileSources = true,
            SupportsDirectorySources = false,
            SupportsProcessSources = true,
            MetadataColumns = ["CompressedSize", "ArchivePath"],
            ExportConfigurationFactory = settings =>
            {
                ArgumentNullException.ThrowIfNull(settings);

                string? outputDirectory = settings.Values.TryGetValue("OutputDirectory", out string? configuredOutputDirectory)
                    ? configuredOutputDirectory
                    : null;

                return new ExportConfiguration
                {
                    OutputDirectory = string.IsNullOrWhiteSpace(outputDirectory)
                        ? GameExtractionSettings.GetDefaultOutputDirectory()
                        : outputDirectory,
                    Overwrite = settings.Values.TryGetValue("Overwrite", out string? overwriteValue)
                        && bool.TryParse(overwriteValue, out bool overwrite)
                        && overwrite,
                    ExportReferences = settings.Values.TryGetValue("ExportReferences", out string? exportReferencesValue)
                        && bool.TryParse(exportReferencesValue, out bool exportReferences)
                        && exportReferences,
                    PreserveDirectoryStructure = !settings.Values.TryGetValue("PreserveDirectoryStructure", out string? preserveDirectoryStructureValue)
                        || (bool.TryParse(preserveDirectoryStructureValue, out bool preserveDirectoryStructure) && preserveDirectoryStructure),
                };
            },
            Settings = new GameExtractionSettings
            {
                Values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["OutputDirectory"] = GameExtractionSettings.GetDefaultOutputDirectory(),
                    ["Overwrite"] = bool.FalseString,
                    ["ExportReferences"] = bool.FalseString,
                    ["PreserveDirectoryStructure"] = bool.TrueString,
                },
            },
            SettingDefinitions =
            [
                new GameExtractionSetting
                {
                    Name = "OutputDirectory",
                    Group = "Export",
                    Label = "Output directory",
                    Type = GameExtractionSettingType.DirectoryPath,
                    DefaultValue = GameExtractionSettings.GetDefaultOutputDirectory(),
                },
                new GameExtractionSetting
                {
                    Name = "Overwrite",
                    Group = "Export",
                    Label = "Overwrite existing files",
                    Type = GameExtractionSettingType.CheckBox,
                    DefaultValue = false,
                },
                new GameExtractionSetting
                {
                    Name = "PreserveDirectoryStructure",
                    Group = "Export",
                    Label = "Preserve directory structure",
                    Type = GameExtractionSettingType.CheckBox,
                    DefaultValue = true,
                },
                new GameExtractionSetting
                {
                    Name = "ExportReferences",
                    Group = "Fuck",
                    Label = "Export referenced assets",
                    Type = GameExtractionSettingType.CheckBox,
                    DefaultValue = false,
                },
                new GameExtractionSetting
                {
                    Name = "ExportReferences",
                    Group = "Models",
                    Label = "Export referenced assets",
                    Type = GameExtractionSettingType.TextBox,
                },
                new GameExtractionSetting
                {
                    Name = "PreserveDirectoryStructure",
                    Group = "Models",
                    Label = "Export Materials",
                    Type = GameExtractionSettingType.CheckBox,
                    DefaultValue = true,
                },
            ],
            About = new AboutConfig
            {
                Description = "A minimal Avalonia shell for ZIP-backed RedFox.GameExtraction sources.",
            },
            PreviewControlFactory = viewModel =>
            {
                if (viewModel.PreviewData is Scene scene)
                {
                    return new ScenePreviewControl
                    {
                        Scene = scene,
                    };
                }

                if (viewModel.PreviewBytes is not null)
                {
                    return new HexBytesPreviewControl
                    {
                        Bytes = viewModel.PreviewBytes,
                    };
                }

                return null;
            },
        });
    }
}
