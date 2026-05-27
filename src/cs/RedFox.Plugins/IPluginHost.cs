namespace RedFox.Plugins;

/// <summary>
/// Represents a language-specific plugin runtime that knows how to load and unload script files.
/// </summary>
/// <remarks>
/// Hosts are registered with a <see cref="PluginManager"/> and selected by file extension. Each host owns its
/// runtime (Python interpreter, Lua state, AssemblyLoadContext, etc.) and is responsible for invoking the script's
/// <c>initialize</c> entry point during <see cref="Load"/> and its <c>deinitialize</c> entry point during
/// <see cref="Unload"/>.
/// </remarks>
public interface IPluginHost
{
    /// <summary>
    /// Gets the unique host name (e.g. <c>"python"</c>, <c>"lua"</c>, <c>"csharp"</c>).
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the file extensions this host handles, including the leading period (e.g. <c>".py"</c>).
    /// </summary>
    IReadOnlyList<string> Extensions { get; }

    /// <summary>
    /// Loads the script identified by <see cref="PluginLoadContext.FilePath"/> into this host and runs its
    /// <c>initialize</c> entry point.
    /// </summary>
    /// <param name="context">The load context populated by the manager.</param>
    void Load(PluginLoadContext context);

    /// <summary>
    /// Runs the script's <c>deinitialize</c> entry point and releases all host-side resources associated with
    /// <paramref name="plugin"/>.
    /// </summary>
    /// <param name="plugin">The plugin being unloaded. The manager has already raised <see cref="Plugin.Unloading"/>.</param>
    void Unload(Plugin plugin);
}
