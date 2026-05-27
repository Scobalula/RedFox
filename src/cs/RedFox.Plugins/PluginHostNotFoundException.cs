namespace RedFox.Plugins;

/// <summary>
/// Thrown when a script file extension does not match any registered <see cref="IPluginHost"/>.
/// </summary>
public sealed class PluginHostNotFoundException : PluginException
{
    /// <summary>
    /// Initializes a new instance for the specified file extension.
    /// </summary>
    /// <param name="extension">The unmatched extension.</param>
    public PluginHostNotFoundException(string extension)
        : base($"No plugin host is registered for extension '{extension}'.") { }
}
