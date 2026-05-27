using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RedFox.Plugins;

namespace RedFox.GameExtraction.UI.ViewModels;

/// <summary>
/// View model backing the plugins management window. Wraps a <see cref="PluginsService"/> with a
/// searchable, observable view over the discovered plugins.
/// </summary>
public sealed partial class PluginsWindowViewModel : ObservableObject, IDisposable
{
    private readonly PluginsService _service;
    private readonly ObservableCollection<PluginRowViewModel> _rows = [];
    private readonly DataGridCollectionView _view;
    private string _filter = string.Empty;

    /// <summary>
    /// Initializes a new <see cref="PluginsWindowViewModel"/>.
    /// </summary>
    /// <param name="service">The plugin service to manage.</param>
    public PluginsWindowViewModel(PluginsService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _view = new DataGridCollectionView(_rows)
        {
            Filter = FilterRow,
        };
        _rows.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmpty));
        _service.DescriptorsChanged += OnDescriptorsChanged;
        Refresh();
    }

    /// <summary>
    /// Gets the plugins collection view bound to the list.
    /// </summary>
    public DataGridCollectionView Plugins => _view;

    /// <summary>
    /// Gets or sets the search text applied to the plugin name.
    /// </summary>
    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    /// <summary>
    /// Gets the directory backing the plugin service.
    /// </summary>

    /// <summary>
    /// Gets a value indicating whether there are no discovered plugins.
    /// </summary>
    public bool IsEmpty => _rows.Count == 0;
    public string PluginsDirectory => _service.PluginsDirectory;

    /// <summary>
    /// Raised when the window should close.
    /// </summary>
    public event Action? CloseRequested;

    /// <summary>
    /// Re-scans the plugins directory.
    /// </summary>
    [RelayCommand]
    public void Refresh()
    {
        _service.Refresh();
        // OnDescriptorsChanged rebuilds the rows; nothing further to do here.
    }

    /// <summary>
    /// Opens the plugin directory in the system file explorer.
    /// </summary>
    [RelayCommand]
    public void BrowseFolder()
    {
        try
        {
            Directory.CreateDirectory(_service.PluginsDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = _service.PluginsDirectory,
                UseShellExecute = true,
            });
        }
        catch
        {
            // Best-effort: opening a folder should never crash the dialog.
        }
    }

    /// <summary>
    /// Closes the plugins window.
    /// </summary>
    [RelayCommand]
    public void Close()
    {
        CloseRequested?.Invoke();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _service.DescriptorsChanged -= OnDescriptorsChanged;
    }

    partial void OnSearchTextChanged(string value)
    {
        _filter = value?.Trim() ?? string.Empty;
        _view.Refresh();
    }

    private void OnDescriptorsChanged(object? sender, EventArgs e)
    {
        Dictionary<string, PluginRowViewModel> existing = _rows.ToDictionary(r => r.Name, StringComparer.OrdinalIgnoreCase);
        _rows.Clear();

        foreach (PluginDescriptor descriptor in _service.Descriptors.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (existing.TryGetValue(descriptor.Name, out PluginRowViewModel? row))
                row.SyncFromDescriptor();
            else
                row = new PluginRowViewModel(_service, descriptor);

            _rows.Add(row);
        }

        _view.Refresh();
    }

    private bool FilterRow(object? item)
    {
        if (string.IsNullOrEmpty(_filter))
            return true;

        return item is PluginRowViewModel row
            && row.Name.Contains(_filter, StringComparison.OrdinalIgnoreCase);
    }
}
