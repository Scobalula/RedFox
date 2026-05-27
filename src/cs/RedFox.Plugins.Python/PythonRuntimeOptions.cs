namespace RedFox.Plugins.Python;

/// <summary>
/// Configures the embedded Python runtime owned by a <see cref="PythonPluginHost"/>.
/// </summary>
public sealed class PythonRuntimeOptions
{
    /// <summary>
    /// Gets or sets the absolute path to the <c>python3X.dll</c> / <c>libpython3.X.so</c> shared library.
    /// When <see langword="null"/>, the value of the <c>PYTHONNET_PYDLL</c> environment variable is used.
    /// </summary>
    public string? PythonDll { get; set; }

    /// <summary>
    /// Gets or sets the absolute path to the Python installation root (<c>PYTHONHOME</c>). May be left
    /// <see langword="null"/> on systems where Python is on the standard search path.
    /// </summary>
    public string? PythonHome { get; set; }

    /// <summary>
    /// Gets the additional directories appended to <c>sys.path</c> after initialization. Plugin script
    /// directories are added automatically per load and do not need to be listed here.
    /// </summary>
    public IList<string> ExtraSearchPaths { get; } = [];

    /// <summary>
    /// Gets or sets the file extensions claimed by the Python host. Defaults to <c>{ ".py" }</c>.
    /// </summary>
    public IList<string> Extensions { get; set; } = [".py"];

    /// <summary>
    /// Gets or sets the host name reported by <see cref="PythonPluginHost.Name"/>. Defaults to <c>"python"</c>.
    /// </summary>
    public string HostName { get; set; } = "python";
}
