namespace RedFox.Plugins;

/// <summary>
/// Describes a single plugin script discovered in a <see cref="PluginsService"/>'s plugins directory.
/// </summary>
public sealed class PluginDescriptor
{
    internal PluginDescriptor(string name, string filePath)
    {
        Name = name;
        FilePath = filePath;
    }

    /// <summary>
    /// Gets the plugin name (the file name without extension).
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the absolute path to the plugin script.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Gets a value indicating whether the plugin is currently loaded.
    /// </summary>
    public bool IsLoaded { get; internal set; }

    /// <summary>
    /// Gets a value indicating whether the plugin is loaded automatically at startup.
    /// </summary>
    public bool IsAutoLoad { get; internal set; }

    /// <summary>
    /// Gets the last error encountered while loading or unloading the plugin, when any.
    /// </summary>
    public Exception? Error { get; internal set; }
}
