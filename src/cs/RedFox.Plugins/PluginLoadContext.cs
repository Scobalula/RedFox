namespace RedFox.Plugins;

/// <summary>
/// Carries information from the <see cref="PluginManager"/> to an <see cref="IPluginHost"/> for a single load operation.
/// </summary>
/// <param name="manager">The plugin manager that owns the operation.</param>
/// <param name="plugin">The plugin handle to populate during load.</param>
/// <param name="filePath">The fully qualified path to the plugin script file.</param>
public sealed class PluginLoadContext(PluginManager manager, Plugin plugin, string filePath)
{
    /// <summary>
    /// Gets the plugin manager performing the load.
    /// </summary>
    public PluginManager Manager { get; } = manager;

    /// <summary>
    /// Gets the plugin handle being populated. Hosts may set <see cref="Plugin.Tag"/> to attach host-specific state.
    /// </summary>
    public Plugin Plugin { get; } = plugin;

    /// <summary>
    /// Gets the absolute path of the script file being loaded.
    /// </summary>
    public string FilePath { get; } = filePath;
}
