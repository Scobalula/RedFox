namespace RedFox.Plugins;

/// <summary>
/// Thrown when attempting to load a plugin with a name that is already registered with the manager.
/// </summary>
/// <param name="name">The conflicting plugin name.</param>
public sealed class PluginAlreadyLoadedException(string name) : PluginException($"A plugin named '{name}' is already loaded. Unload or reload it first.");
