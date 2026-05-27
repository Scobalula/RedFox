namespace RedFox.Plugins;

/// <summary>
/// Base exception type for plugin failures surfaced by <see cref="PluginManager"/> or by a host implementation.
/// </summary>
public class PluginException : Exception
{
    /// <summary>
    /// Initializes a new instance with the specified message.
    /// </summary>
    public PluginException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance with the specified message and inner exception.
    /// </summary>
    public PluginException(string message, Exception innerException) : base(message, innerException) { }
}
