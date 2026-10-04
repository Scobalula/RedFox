using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace RedFox.GameExtraction.UI.Views;

/// <summary>
/// Asks the user to support the application with a donation.
/// </summary>
public partial class DonateWindow : Window
{
    private string? _url;

    /// <summary>
    /// Creates an uninitialized donate window.
    /// </summary>
    public DonateWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Populates the window from application configuration.
    /// </summary>
    /// <param name="config">Configuration containing the application and donation details to display.</param>
    /// <param name="isFirstLaunch">Whether the window is the one-time first launch prompt.</param>
    public void Initialize(GameExtractionConfig config, bool isFirstLaunch)
    {
        ShownOnceNotice.IsVisible = isFirstLaunch;
        _url = config.Donation?.Url;
        AppTitle.Text = config.WindowTitle;
        AppDescription.Text = config.About?.Description ?? config.Description;

        AppIcon.Source = AboutWindow.LoadIcon(config.SidebarIconPath ?? config.IconPath);
        LogoSection.IsVisible = AppIcon.Source is not null;
        HeartIcon.IsVisible = AppIcon.Source is null;

        if (string.IsNullOrWhiteSpace(config.Donation?.Message))
        {
            MessageSection.IsVisible = false;
        }
        else
        {
            DonationMessage.Text = config.Donation.Message;
        }
    }

    private void OnDonateClick(object? sender, RoutedEventArgs e)
    {
        if (_url is not null)
        {
            try
            {
                Process.Start(new ProcessStartInfo(_url) { UseShellExecute = true })?.Dispose();
            }
            catch
            {
            }
        }

        Close();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
