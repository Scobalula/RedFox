namespace RedFox.Plugins;

/// <summary>
/// Thrown when a plugin name is requested but no plugin with that name has been loaded.
/// </summary>
public sealed class PluginNotFoundException : PluginException
{
    /// <summary>
    /// Initializes a new instance for the specified plugin name.
    /// </summary>
    /// <param name="name">The plugin name that could not be resolved.</param>
    public PluginNotFoundException(string name)
        : base($"No plugin named '{name}' is currently loaded.") { }
}
