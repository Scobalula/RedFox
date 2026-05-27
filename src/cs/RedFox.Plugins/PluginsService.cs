using System.Text.Json;
using System.Text.Json.Serialization;

namespace RedFox.Plugins;

/// <summary>
/// Host-agnostic plugin coordinator: discovers plugin scripts in a single directory, tracks load and
/// auto-load state across runs, and exposes the underlying <see cref="PluginManager"/> for hook registration.
/// </summary>
/// <remarks>
/// <para>The service owns a single <see cref="PluginManager"/> and is itself language- and application-neutral.
/// Callers register whichever <see cref="IPluginHost"/> implementations they want to support (e.g. a Python or
/// Lua host) via the constructor or by accessing <see cref="Manager"/>.</para>
/// <para>Application services intended for plugin consumption (translators, name lists, etc.) should be placed
/// on <see cref="PluginManager.Services"/> via <see cref="Manager"/>.</para>
/// <para>Auto-load preferences are persisted to <c>plugins.json</c> alongside the plugin directory.</para>
/// </remarks>
public sealed class PluginsService : IDisposable
{
    private const string StateFileName = "plugins.json";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true,
    };

    private readonly PluginManager _manager;
    private readonly Dictionary<string, PluginDescriptor> _descriptors = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _autoLoadNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _statePath;
    private bool _disposed;

    /// <summary>
    /// Initializes a new <see cref="PluginsService"/> rooted at <paramref name="pluginsDirectory"/> with no
    /// pre-registered hosts. Register hosts through <see cref="Manager"/> before calling <see cref="Refresh"/>
    /// or <see cref="LoadAutoLoaded"/>.
    /// </summary>
    /// <param name="pluginsDirectory">The directory scanned for plugin scripts. Created if missing.</param>
    public PluginsService(string pluginsDirectory)
        : this(pluginsDirectory, (IEnumerable<IPluginHost>?)null)
    {
    }

    /// <summary>
    /// Initializes a new <see cref="PluginsService"/> rooted at <paramref name="pluginsDirectory"/>, pre-registering
    /// the supplied <paramref name="hosts"/>.
    /// </summary>
    /// <param name="pluginsDirectory">The directory scanned for plugin scripts. Created if missing.</param>
    /// <param name="hosts">The plugin hosts to register on the underlying <see cref="PluginManager"/>.</param>
    public PluginsService(string pluginsDirectory, params IPluginHost[] hosts)
        : this(pluginsDirectory, (IEnumerable<IPluginHost>?)hosts)
    {
    }

    /// <summary>
    /// Initializes a new <see cref="PluginsService"/> rooted at <paramref name="pluginsDirectory"/>, pre-registering
    /// the supplied <paramref name="hosts"/>.
    /// </summary>
    /// <param name="pluginsDirectory">The directory scanned for plugin scripts. Created if missing.</param>
    /// <param name="hosts">The plugin hosts to register on the underlying <see cref="PluginManager"/>. May be <see langword="null"/>.</param>
    public PluginsService(string pluginsDirectory, IEnumerable<IPluginHost>? hosts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginsDirectory);

        PluginsDirectory = Path.GetFullPath(pluginsDirectory);
        Directory.CreateDirectory(PluginsDirectory);
        _statePath = Path.Combine(PluginsDirectory, StateFileName);

        _manager = new PluginManager();
        if (hosts is not null)
        {
            foreach (IPluginHost host in hosts)
            {
                if (host is not null)
                    _manager.RegisterHost(host);
            }
        }

        LoadState();
    }

    /// <summary>
    /// Gets the absolute path of the plugin directory.
    /// </summary>
    public string PluginsDirectory { get; }

    /// <summary>
    /// Gets the underlying plugin manager. Use it to register additional hosts or to expose services to plugins
    /// via <see cref="PluginManager.Services"/>.
    /// </summary>
    public PluginManager Manager => _manager;

    /// <summary>
    /// Gets the discovered plugins. The collection is refreshed by <see cref="Refresh"/>.
    /// </summary>
    public IReadOnlyCollection<PluginDescriptor> Descriptors => _descriptors.Values;

    /// <summary>
    /// Raised whenever the descriptor list or any descriptor's state changes.
    /// </summary>
    public event EventHandler? DescriptorsChanged;

    /// <summary>
    /// Rescans <see cref="PluginsDirectory"/>, refreshing the descriptor list. Existing descriptors retain
    /// their load and auto-load state.
    /// </summary>
    public void Refresh()
    {
        IReadOnlyList<string> extensions = CollectExtensions();
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        foreach (string filePath in EnumeratePluginFiles(extensions))
        {
            string name = Path.GetFileNameWithoutExtension(filePath);
            seen.Add(name);

            if (!_descriptors.TryGetValue(name, out PluginDescriptor? descriptor))
            {
                descriptor = new PluginDescriptor(name, filePath)
                {
                    IsAutoLoad = _autoLoadNames.Contains(name),
                    IsLoaded = _manager.TryGetPlugin(name, out _),
                };
                _descriptors[name] = descriptor;
            }
            else
            {
                descriptor.IsLoaded = _manager.TryGetPlugin(name, out _);
                descriptor.IsAutoLoad = _autoLoadNames.Contains(name);
            }
        }

        foreach (string name in _descriptors.Keys.ToArray())
        {
            if (!seen.Contains(name))
                _descriptors.Remove(name);
        }

        RaiseChanged();
    }

    /// <summary>
    /// Loads every descriptor whose <see cref="PluginDescriptor.IsAutoLoad"/> flag is set. Failures are recorded
    /// on the descriptor but do not abort the batch.
    /// </summary>
    public void LoadAutoLoaded()
    {
        Refresh();
        foreach (PluginDescriptor descriptor in _descriptors.Values)
        {
            if (descriptor.IsAutoLoad && !descriptor.IsLoaded)
                TryLoadInternal(descriptor);
        }
        RaiseChanged();
    }

    /// <summary>
    /// Loads the plugin described by <paramref name="descriptor"/>.
    /// </summary>
    public bool Load(PluginDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (descriptor.IsLoaded)
            return true;

        bool ok = TryLoadInternal(descriptor);
        RaiseChanged();
        return ok;
    }

    /// <summary>
    /// Unloads the plugin described by <paramref name="descriptor"/>.
    /// </summary>
    public bool Unload(PluginDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (!descriptor.IsLoaded)
            return false;

        try
        {
            _manager.Unload(descriptor.Name);
            descriptor.IsLoaded = false;
            descriptor.Error = null;
        }
        catch (Exception ex)
        {
            descriptor.IsLoaded = false;
            descriptor.Error = ex;
        }

        RaiseChanged();
        return true;
    }

    /// <summary>
    /// Sets whether <paramref name="descriptor"/> should be auto-loaded on startup. Persisted immediately.
    /// Enabling auto-load also loads the plugin if it is not already loaded so the user sees an immediate effect.
    /// </summary>
    public void SetAutoLoad(PluginDescriptor descriptor, bool value)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        bool changed = descriptor.IsAutoLoad != value;
        if (changed)
        {
            descriptor.IsAutoLoad = value;
            if (value)
                _autoLoadNames.Add(descriptor.Name);
            else
                _autoLoadNames.Remove(descriptor.Name);

            SaveState();
        }

        if (value && !descriptor.IsLoaded)
            TryLoadInternal(descriptor);

        if (changed || value)
            RaiseChanged();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _manager.Dispose();
    }

    private bool TryLoadInternal(PluginDescriptor descriptor)
    {
        try
        {
            _manager.Load(descriptor.FilePath, descriptor.Name);
            descriptor.IsLoaded = true;
            descriptor.Error = null;
            return true;
        }
        catch (Exception ex)
        {
            descriptor.IsLoaded = false;
            descriptor.Error = ex;
            return false;
        }
    }

    private IReadOnlyList<string> CollectExtensions()
    {
        List<string> extensions = [];
        foreach (IPluginHost host in _manager.Hosts)
            extensions.AddRange(host.Extensions);
        return extensions;
    }

    private IEnumerable<string> EnumeratePluginFiles(IReadOnlyList<string> extensions)
    {
        if (!Directory.Exists(PluginsDirectory))
            yield break;

        foreach (string file in Directory.EnumerateFiles(PluginsDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            string ext = Path.GetExtension(file);
            for (int i = 0; i < extensions.Count; i++)
            {
                if (string.Equals(extensions[i], ext, StringComparison.OrdinalIgnoreCase))
                {
                    yield return file;
                    break;
                }
            }
        }
    }

    private void LoadState()
    {
        if (!File.Exists(_statePath))
            return;

        try
        {
            using FileStream stream = File.OpenRead(_statePath);
            PluginsState? state = JsonSerializer.Deserialize<PluginsState>(stream, SerializerOptions);
            if (state?.AutoLoad is null)
                return;

            foreach (string name in state.AutoLoad)
                _autoLoadNames.Add(name);
        }
        catch
        {
            // Corrupt state should not prevent the application from starting.
        }
    }

    private void SaveState()
    {
        try
        {
            using FileStream stream = File.Create(_statePath);
            PluginsState state = new() { AutoLoad = [.. _autoLoadNames] };
            JsonSerializer.Serialize(stream, state, SerializerOptions);
        }
        catch
        {
            // Persistence failures should not crash the host application.
        }
    }

    private void RaiseChanged() => DescriptorsChanged?.Invoke(this, EventArgs.Empty);

    private sealed class PluginsState
    {
        [JsonPropertyName("autoLoad")]
        public List<string>? AutoLoad { get; set; }
    }
}
