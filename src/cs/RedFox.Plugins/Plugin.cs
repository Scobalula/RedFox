namespace RedFox.Plugins;

/// <summary>
/// Represents a single loaded script as seen by the host application. Scripts attach to <see cref="Unloading"/>
/// to dispose any C# objects they created during initialization.
/// </summary>
public sealed class Plugin
{
    private readonly Dictionary<string, object?> _properties = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new plugin handle. Called by the <see cref="PluginManager"/>; hosts should not invoke this directly.
    /// </summary>
    /// <param name="name">The unique plugin name.</param>
    /// <param name="filePath">The fully qualified path of the script file.</param>
    /// <param name="host">The host that owns the plugin.</param>
    internal Plugin(string name, string filePath, IPluginHost host, PluginManager manager)
    {
        Name = name;
        FilePath = filePath;
        Host = host;
        Manager = manager;
    }

    /// <summary>
    /// Gets the manager that owns this plugin. Scripts use <see cref="PluginManager.Services"/> on this
    /// reference to obtain application services (e.g. translator registries) from the host process.
    /// </summary>
    public PluginManager Manager { get; }

    /// <summary>
    /// Gets the unique plugin name (defaults to the script file name without extension).
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the absolute path of the script file backing this plugin.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Gets the host that owns this plugin.
    /// </summary>
    public IPluginHost Host { get; }

    /// <summary>
    /// Gets the current lifecycle state of the plugin.
    /// </summary>
    public PluginState State { get; internal set; } = PluginState.Loading;

    /// <summary>
    /// Gets the exception captured if the plugin entered <see cref="PluginState.Faulted"/>.
    /// </summary>
    public Exception? Error { get; internal set; }

    /// <summary>
    /// Gets or sets a host-specific opaque value (e.g. the Python module object backing the plugin).
    /// </summary>
    public object? Tag { get; set; }

    /// <summary>
    /// Gets a free-form bag of script-defined properties. Useful for sharing values between the script and host code.
    /// </summary>
    public IDictionary<string, object?> Properties => _properties;

    /// <summary>
    /// Raised immediately before the host unloads the script. Handlers must dispose or unregister any C# objects
    /// the script created (translators, event subscriptions, native handles, etc.). Handler exceptions are
    /// surfaced to the manager and do not abort unloading of the remaining handlers.
    /// </summary>
    public event EventHandler<PluginEventArgs>? Unloading;

    internal void RaiseUnloading()
    {
        EventHandler<PluginEventArgs>? handlers = Unloading;
        if (handlers is null)
            return;

        List<Exception>? failures = null;
        PluginEventArgs args = new(this);
        foreach (Delegate handler in handlers.GetInvocationList())
        {
            try
            {
                ((EventHandler<PluginEventArgs>)handler).Invoke(this, args);
            }
            catch (Exception ex)
            {
                (failures ??= []).Add(ex);
            }
        }

        Unloading = null;
        if (failures is not null)
            throw new AggregateException($"One or more Unloading handlers for plugin '{Name}' threw.", failures);
    }
}
