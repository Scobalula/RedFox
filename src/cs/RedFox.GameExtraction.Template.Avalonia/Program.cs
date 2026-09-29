using RedFox.GameExtraction.Template;
using RedFox.GameExtraction.UI;
using RedFox.GameExtraction.UI.Controls;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.GameExtraction.Template.Avalonia;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ScenePreviewControl scenePreviewControl = new();

        GameExtractionApp.Run(new GameExtractionConfig
        {
            AssetManagerFactory = TemplateAssetManagerFactory.Create,
            WindowTitle = "RedFox ZIP Explorer",
            SidebarTitle = "ZIP Explorer",
            Description = "Open ZIP archives and export raw entries.",
            AppName = "RedFoxZipExplorer",
            Version = "1.0.0",
            AccentColor = "#007000",
            FileFilter = "All Files|*.*",
            SupportsFileSources = true,
            SupportsDirectorySources = false,
            SupportsProcessSources = true,
            EnableDirectoryView = true,
            MetadataColumns = ["CompressedSize", "ArchivePath"],
            ExportConfigurationFactory = TemplateSettings.CreateExportConfiguration,
            Settings = TemplateSettings.CreateDefaults(),
            SettingDefinitions = TemplateSettings.Definitions,
            About = new AboutConfig
            {
                Description = "A minimal Avalonia shell for ZIP-backed RedFox.GameExtraction sources.",
            },
            PreviewContentFactory = viewModel =>
            {
                var scene = viewModel.PreviewData switch
                {
                    Scene single => single,
                    Scene[] { Length: > 0 } scenes => scenes[0],
                    _ => null,
                };

                if (scene is not null)
                {
                    if (scene.GetDescendants<Mesh>().Length == 0 && scene.GetDescendants<SkeletonAnimation>().Length > 0 && scenePreviewControl.TryAppendAnimationScene(scene))
                    {
                        return scenePreviewControl;
                    }

                    scenePreviewControl.Scene = scene;
                    return scenePreviewControl;
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
