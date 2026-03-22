using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;

namespace RedFox.GameExtraction.UI.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
    }

    public void Initialize(GameExtractionConfig config)
    {
        AppTitle.Text = config.WindowTitle;
        AppVersion.Text = $"Version {config.Version}";

        // Icon
        var icon = LoadIcon(config.SidebarIconPath ?? config.IconPath);
        if (icon is not null)
        {
            AppIcon.Source = icon;
        }
        else
        {
            AppIcon.IsVisible = false;
        }

        // Description
        if (!string.IsNullOrEmpty(config.About?.Description))
        {
            AppDescription.Text = config.About.Description;
        }
        else
        {
            AppDescription.IsVisible = false;
        }

        // Links
        if (config.About?.Links is { Count: > 0 } links)
        {
            LinksPanel.ItemsSource = links;
        }
        else
        {
            LinksSection.IsVisible = false;
        }
    }

    private void OnLinkClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string url })
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch
            {
                // Ignore failures to open URL
            }
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private static Bitmap? LoadIcon(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        var fullPath = Path.IsPathRooted(path)
            ? path
            : Path.Combine(AppContext.BaseDirectory, path);

        if (!File.Exists(fullPath))
            return null;

        try
        {
            return new Bitmap(fullPath);
        }
        catch
        {
            return null;
        }
    }
}
