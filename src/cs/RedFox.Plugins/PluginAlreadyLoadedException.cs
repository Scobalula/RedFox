namespace RedFox.Plugins;

/// <summary>
/// Thrown when attempting to load a plugin with a name that is already registered with the manager.
/// </summary>
public sealed class PluginAlreadyLoadedException : PluginException
{
    /// <summary>
    /// Initializes a new instance for the specified plugin name.
    /// </summary>
    /// <param name="name">The conflicting plugin name.</param>
    public PluginAlreadyLoadedException(string name)
        : base($"A plugin named '{name}' is already loaded. Unload or reload it first.") { }
}
