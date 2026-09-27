namespace RedFox.Plugins;

/// <summary>
/// Thrown when a plugin name is requested but no plugin with that name has been loaded.
/// </summary>
/// <param name="name">The plugin name that could not be resolved.</param>
public sealed class PluginNotFoundException(string name) : PluginException($"No plugin named '{name}' is currently loaded.");
