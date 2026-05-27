using System.Diagnostics.CodeAnalysis;

namespace RedFox.Plugins;

/// <summary>
/// Discovers, loads, and unloads scripts across one or more registered <see cref="IPluginHost"/> instances.
/// </summary>
/// <remarks>
/// The manager is responsible for plugin lifecycle bookkeeping (naming, host selection, state transitions) and for
/// raising <see cref="Plugin.Unloading"/> before delegating teardown to the owning host. Scripts hook
/// <see cref="Plugin.Unloading"/> to dispose any C# objects they created during initialization &#8211; the manager
/// has no way to know where those objects live.
/// </remarks>
public sealed class PluginManager : IDisposable
{
    private readonly Dictionary<string, IPluginHost> _hostsByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IPluginHost> _hostsByExtension = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Plugin> _plugins = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, object?> _services = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets a read-only view of all currently loaded plugins.
    /// </summary>
    public IReadOnlyCollection<Plugin> Plugins => _plugins.Values;

    /// <summary>
    /// Gets a read-only view of all registered hosts.
    /// </summary>
    public IReadOnlyCollection<IPluginHost> Hosts => _hostsByName.Values;

    /// <summary>
    /// Gets a free-form, application-wide bag of services that scripts may request from their host (e.g. a
    /// <c>SceneTranslatorManager</c>). Use it to expose application objects without leaking them through every host API.
    /// </summary>
    public IDictionary<string, object?> Services => _services;

    /// <summary>
    /// Raised after a plugin has finished loading.
    /// </summary>
    public event EventHandler<PluginEventArgs>? PluginLoaded;

    /// <summary>
    /// Raised after a plugin has finished unloading (including unload failures, in which case the plugin enters
    /// <see cref="PluginState.Faulted"/>).
    /// </summary>
    public event EventHandler<PluginEventArgs>? PluginUnloaded;

    /// <summary>
    /// Registers <paramref name="host"/>, indexed by name and by each of its extensions.
    /// </summary>
    /// <param name="host">The host to register.</param>
    /// <returns>This manager, for chaining.</returns>
    public PluginManager RegisterHost(IPluginHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        _hostsByName[host.Name] = host;
        foreach (string extension in host.Extensions)
            _hostsByExtension[extension] = host;

        return this;
    }

    /// <summary>
    /// Attempts to resolve a host by its name.
    /// </summary>
    public bool TryGetHost(string name, [NotNullWhen(true)] out IPluginHost? host) =>
        _hostsByName.TryGetValue(name, out host);

    /// <summary>
    /// Attempts to resolve the plugin with the given name.
    /// </summary>
    public bool TryGetPlugin(string name, [NotNullWhen(true)] out Plugin? plugin) =>
        _plugins.TryGetValue(name, out plugin);

    /// <summary>
    /// Loads a script, selecting the host by file extension and using the file name (without extension) as the plugin name.
    /// </summary>
    public Plugin Load(string filePath) => Load(filePath, name: null, host: null);

    /// <summary>
    /// Loads a script with an explicit plugin name, selecting the host by file extension.
    /// </summary>
    public Plugin Load(string filePath, string? name) => Load(filePath, name, host: null);

    /// <summary>
    /// Loads a script, optionally overriding both the plugin name and the host.
    /// </summary>
    /// <param name="filePath">Path to the script file. May be relative; it is resolved against the current directory.</param>
    /// <param name="name">Optional plugin name. Defaults to the file name without extension.</param>
    /// <param name="host">Optional host override. Defaults to the host registered for the file's extension.</param>
    /// <returns>The loaded plugin.</returns>
    /// <exception cref="FileNotFoundException">The script file does not exist.</exception>
    /// <exception cref="PluginHostNotFoundException">No host matches the file extension and no override was supplied.</exception>
    /// <exception cref="PluginAlreadyLoadedException">A plugin with the resolved name is already loaded.</exception>
    /// <exception cref="PluginException">Wraps any failure thrown by the host while loading.</exception>
    public Plugin Load(string filePath, string? name, IPluginHost? host)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        string fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Plugin script not found: {fullPath}", fullPath);

        string pluginName = string.IsNullOrWhiteSpace(name) ? Path.GetFileNameWithoutExtension(fullPath) : name;
        if (_plugins.ContainsKey(pluginName))
            throw new PluginAlreadyLoadedException(pluginName);

        string extension = Path.GetExtension(fullPath);
        IPluginHost resolvedHost = host
            ?? (_hostsByExtension.TryGetValue(extension, out IPluginHost? matched)
                ? matched
                : throw new PluginHostNotFoundException(extension));

        Plugin plugin = new(pluginName, fullPath, resolvedHost, this);
        _plugins.Add(pluginName, plugin);

        try
        {
            resolvedHost.Load(new PluginLoadContext(this, plugin, fullPath));
            plugin.State = PluginState.Loaded;
        }
        catch (Exception ex)
        {
            plugin.State = PluginState.Faulted;
            plugin.Error = ex;
            _plugins.Remove(pluginName);
            throw new PluginException($"Failed to load plugin '{pluginName}' from '{fullPath}'.", ex);
        }

        PluginLoaded?.Invoke(this, new PluginEventArgs(plugin));
        return plugin;
    }

    /// <summary>
    /// Unloads the plugin with the specified name.
    /// </summary>
    /// <param name="name">The plugin name.</param>
    /// <returns><see langword="true"/> if a plugin was found and unloaded; <see langword="false"/> if no such plugin exists.</returns>
    /// <exception cref="PluginException">Wraps any failure raised by a script's <c>Unloading</c> handler or by the host's teardown.</exception>
    public bool Unload(string name)
    {
        if (!_plugins.TryGetValue(name, out Plugin? plugin))
            return false;

        _plugins.Remove(name);
        Exception? captured = null;

        plugin.State = PluginState.Unloading;
        try
        {
            plugin.RaiseUnloading();
        }
        catch (Exception ex)
        {
            captured = ex;
        }

        try
        {
            plugin.Host.Unload(plugin);
        }
        catch (Exception ex)
        {
            captured = captured is null
                ? ex
                : new AggregateException(captured, ex);
        }

        if (captured is null)
        {
            plugin.State = PluginState.Unloaded;
        }
        else
        {
            plugin.State = PluginState.Faulted;
            plugin.Error = captured;
        }

        PluginUnloaded?.Invoke(this, new PluginEventArgs(plugin));

        if (captured is not null)
            throw new PluginException($"Failed to unload plugin '{name}'.", captured);

        return true;
    }

    /// <summary>
    /// Unloads (if loaded) and re-loads the named plugin from its original file path.
    /// </summary>
    /// <param name="name">The plugin name.</param>
    /// <returns>The freshly loaded plugin.</returns>
    /// <exception cref="PluginNotFoundException">The plugin was not loaded before the call.</exception>
    public Plugin Reload(string name)
    {
        if (!_plugins.TryGetValue(name, out Plugin? plugin))
            throw new PluginNotFoundException(name);

        string filePath = plugin.FilePath;
        IPluginHost host = plugin.Host;
        Unload(name);
        return Load(filePath, name, host);
    }

    /// <summary>
    /// Unloads all currently loaded plugins. Exceptions raised during teardown are aggregated and rethrown.
    /// </summary>
    public void UnloadAll()
    {
        List<Exception>? failures = null;
        foreach (string name in _plugins.Keys.ToArray())
        {
            try
            {
                Unload(name);
            }
            catch (Exception ex)
            {
                (failures ??= []).Add(ex);
            }
        }

        if (failures is not null)
            throw new AggregateException("One or more plugins failed to unload.", failures);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        UnloadAll();
        foreach (IPluginHost host in _hostsByName.Values)
        {
            if (host is IDisposable disposable)
                disposable.Dispose();
        }
        _hostsByName.Clear();
        _hostsByExtension.Clear();
    }
}
