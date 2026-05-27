namespace RedFox.Plugins;

/// <summary>
/// Describes the lifecycle state of a <see cref="Plugin"/>.
/// </summary>
public enum PluginState
{
    /// <summary>
    /// The plugin object exists but its host has not finished loading the script.
    /// </summary>
    Loading,

    /// <summary>
    /// The plugin script has been loaded and its <c>initialize</c> entry point has run successfully.
    /// </summary>
    Loaded,

    /// <summary>
    /// The plugin is being torn down. <see cref="Plugin.Unloading"/> handlers are running.
    /// </summary>
    Unloading,

    /// <summary>
    /// The plugin has been fully unloaded and any host-side resources have been released.
    /// </summary>
    Unloaded,

    /// <summary>
    /// The plugin failed during load or unload. Inspect <see cref="Plugin.Error"/> for details.
    /// </summary>
    Faulted,
}
