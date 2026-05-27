using Avalonia.Controls;
using RedFox.Plugins;
using RedFox.GameExtraction.UI.ViewModels;

namespace RedFox.GameExtraction.UI.Views;

/// <summary>
/// Window for managing discovered plugins (load/unload and auto-load).
/// </summary>
public partial class PluginsWindow : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginsWindow"/> class.
    /// </summary>
    public PluginsWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Initializes the window with a plugin service.
    /// </summary>
    /// <param name="service">The plugin service to expose.</param>
    public void Initialize(PluginsService service)
    {
        ArgumentNullException.ThrowIfNull(service);

        PluginsWindowViewModel viewModel = new(service);
        DataContext = viewModel;

        viewModel.CloseRequested += Close;
        Closed += (_, _) => viewModel.Dispose();
    }
}
