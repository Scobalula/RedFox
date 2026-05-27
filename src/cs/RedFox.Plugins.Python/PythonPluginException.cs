namespace RedFox.Plugins.Python;

/// <summary>
/// Exception raised for failures originating in the Python runtime owned by <see cref="PythonPluginHost"/>.
/// </summary>
public sealed class PythonPluginException : PluginException
{
    /// <summary>
    /// Initializes a new instance with the specified message.
    /// </summary>
    public PythonPluginException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance with the specified message and inner exception.
    /// </summary>
    public PythonPluginException(string message, Exception innerException) : base(message, innerException) { }
}
