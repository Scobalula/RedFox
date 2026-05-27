using CommunityToolkit.Mvvm.ComponentModel;
using RedFox.Plugins;

namespace RedFox.GameExtraction.UI.ViewModels;

/// <summary>
/// Observable row wrapper for a <see cref="PluginDescriptor"/> displayed in the plugins window.
/// </summary>
public sealed partial class PluginRowViewModel : ObservableObject
{
    private readonly PluginsService _service;
    private bool _suppressCallbacks;

    /// <summary>
    /// Initializes a new <see cref="PluginRowViewModel"/>.
    /// </summary>
    /// <param name="service">The owning plugin service.</param>
    /// <param name="descriptor">The descriptor to display.</param>
    public PluginRowViewModel(PluginsService service, PluginDescriptor descriptor)
    {
        _service = service;
        Descriptor = descriptor;
        SyncFromDescriptor();
    }

    /// <summary>
    /// Gets the wrapped descriptor.
    /// </summary>
    public PluginDescriptor Descriptor { get; }

    /// <summary>
    /// Gets the plugin name.
    /// </summary>
    public string Name => Descriptor.Name;

    /// <summary>
    /// Gets the plugin file path.
    /// </summary>
    public string FilePath => Descriptor.FilePath;

    /// <summary>
    /// Gets or sets a value indicating whether the plugin is currently loaded.
    /// Toggling triggers a load or unload through the plugin service.
    /// </summary>
    [ObservableProperty]
    public partial bool IsLoaded { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the plugin should auto-load on startup.
    /// Persisted immediately when toggled.
    /// </summary>
    [ObservableProperty]
    public partial bool IsAutoLoad { get; set; }

    /// <summary>
    /// Gets the most recent load or unload error message, if any.
    /// </summary>
    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    /// <summary>
    /// Refreshes the row from the descriptor's current state without firing toggle callbacks.
    /// </summary>
    public void SyncFromDescriptor()
    {
        _suppressCallbacks = true;
        try
        {
            IsLoaded = Descriptor.IsLoaded;
            IsAutoLoad = Descriptor.IsAutoLoad;
            ErrorMessage = Descriptor.Error?.InnerException?.Message ?? Descriptor.Error?.Message;
        }
        finally
        {
            _suppressCallbacks = false;
        }
    }

    partial void OnIsLoadedChanged(bool value)
    {
        if (_suppressCallbacks)
            return;

        if (value)
            _service.Load(Descriptor);
        else
            _service.Unload(Descriptor);

        SyncFromDescriptor();
    }

    partial void OnIsAutoLoadChanged(bool value)
    {
        if (_suppressCallbacks)
            return;

        _service.SetAutoLoad(Descriptor, value);
        SyncFromDescriptor();
    }
}
