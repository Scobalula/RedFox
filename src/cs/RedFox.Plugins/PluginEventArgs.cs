namespace RedFox.Plugins;

/// <summary>
/// Event data carrying the affected <see cref="Plugin"/>.
/// </summary>
/// <param name="plugin">The plugin associated with the event.</param>
public sealed class PluginEventArgs(Plugin plugin) : EventArgs
{
    /// <summary>
    /// Gets the plugin that raised the event.
    /// </summary>
    public Plugin Plugin { get; } = plugin;
}
