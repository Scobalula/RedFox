namespace RedFox.Plugins;

/// <summary>
/// Thrown when a script file extension does not match any registered <see cref="IPluginHost"/>.
/// </summary>
/// <param name="extension">The unmatched extension.</param>
public sealed class PluginHostNotFoundException(string extension) : PluginException($"No plugin host is registered for extension '{extension}'.");
