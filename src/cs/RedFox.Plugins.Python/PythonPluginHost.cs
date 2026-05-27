using System.Diagnostics.CodeAnalysis;
using Python.Runtime;

namespace RedFox.Plugins.Python;

/// <summary>
/// A <see cref="IPluginHost"/> that loads <c>.py</c> scripts with Python.NET (pythonnet) and dispatches the
/// Maya-style <c>initialize(plugin)</c> / <c>deinitialize(plugin)</c> lifecycle entry points.
/// </summary>
/// <remarks>
/// <para>
/// Pythonnet supports exactly one interpreter per process. The first <see cref="PythonPluginHost"/> instance
/// constructed in the process owns interpreter initialization; subsequent instances reuse it. Disposing the host
/// stops accepting new loads but leaves the shared interpreter running for any remaining hosts.
/// </para>
/// <para>
/// Scripts receive the <see cref="Plugin"/> instance as the single argument to <c>initialize</c>. Use
/// <see cref="Plugin.Unloading"/> to dispose any C# objects the script created during init (translators,
/// registrations, native handles, etc.). The host always invokes <c>deinitialize(plugin)</c> after that event
/// has fired, so either mechanism may be used.
/// </para>
/// </remarks>
public sealed class PythonPluginHost : IPluginHost, IDisposable
{
    private static readonly Lock _engineLock = new();
    private static IntPtr _mainThreadState = IntPtr.Zero;
    private static int _hostCount;

    private readonly PythonRuntimeOptions _options;
    private bool _disposed;

    /// <summary>
    /// Initializes a new Python host with default options.
    /// </summary>
    public PythonPluginHost() : this(new PythonRuntimeOptions()) { }

    /// <summary>
    /// Initializes a new Python host with the supplied <paramref name="options"/>.
    /// </summary>
    /// <param name="options">Runtime configuration. The instance is captured by reference.</param>
    public PythonPluginHost(PythonRuntimeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        EnsureEngineStarted(options);
    }

    /// <inheritdoc />
    public string Name => _options.HostName;

    /// <inheritdoc />
    public IReadOnlyList<string> Extensions => (IReadOnlyList<string>)_options.Extensions;

    /// <inheritdoc />
    public void Load(PluginLoadContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ObjectDisposedException.ThrowIf(_disposed, this);

        string scriptDirectory = Path.GetDirectoryName(context.FilePath) ?? string.Empty;
        string source = File.ReadAllText(context.FilePath);

        using (Py.GIL())
        {
            if (!string.IsNullOrEmpty(scriptDirectory))
                AppendSysPath(scriptDirectory);

            PyModule scope = Py.CreateScope(context.Plugin.Name);
            try
            {
                scope.Set("__file__", context.FilePath.ToPython());
                scope.Exec(source);

                using PyObject pyPlugin = context.Plugin.ToPython();
                if (scope.Contains("initialize"))
                    scope.InvokeMethod("initialize", pyPlugin);

                context.Plugin.Tag = scope;
            }
            catch
            {
                scope.Dispose();
                throw;
            }
        }
    }

    /// <inheritdoc />
    public void Unload(Plugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);

        if (plugin.Tag is not PyModule scope)
            return;

        plugin.Tag = null;
        Exception? captured = null;

        using (Py.GIL())
        {
            try
            {
                if (scope.Contains("deinitialize"))
                {
                    using PyObject pyPlugin = plugin.ToPython();
                    scope.InvokeMethod("deinitialize", pyPlugin);
                }
            }
            catch (Exception ex)
            {
                captured = ex;
            }

            scope.Dispose();

            try
            {
                using PyObject sysModules = Py.Import("sys").GetAttr("modules");
                if (sysModules.HasAttr("pop"))
                    sysModules.InvokeMethod("pop", plugin.Name.ToPython(), PyObject.None);
            }
            catch
            {
                // Removing the module from sys.modules is best-effort; failure does not affect unload semantics.
            }

            try
            {
                using PyObject gc = Py.Import("gc");
                gc.InvokeMethod("collect");
            }
            catch
            {
                // Same as above.
            }
        }

        if (captured is not null)
            throw new PythonPluginException($"Python deinitialize() failed for plugin '{plugin.Name}'.", captured);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        lock (_engineLock)
        {
            if (--_hostCount > 0 || _mainThreadState == IntPtr.Zero)
                return;

            PythonEngine.EndAllowThreads(_mainThreadState);
            PythonEngine.Shutdown();
            _mainThreadState = IntPtr.Zero;
        }
    }

    private static void EnsureEngineStarted(PythonRuntimeOptions options)
    {
        lock (_engineLock)
        {
            _hostCount++;
            if (PythonEngine.IsInitialized)
                return;

            string? dll = !string.IsNullOrWhiteSpace(options.PythonDll)
                ? options.PythonDll
                : PythonResolver.Resolve();

            if (string.IsNullOrWhiteSpace(dll))
                throw new PythonPluginException(
                    "Could not locate a Python runtime DLL. Install Python 3 or set PythonRuntimeOptions.PythonDll / the PYTHONNET_PYDLL environment variable.");

            Runtime.PythonDLL = dll;

            if (!string.IsNullOrWhiteSpace(options.PythonHome))
                PythonEngine.PythonHome = options.PythonHome;

            PythonEngine.Initialize();
            _mainThreadState = PythonEngine.BeginAllowThreads();

            if (options.ExtraSearchPaths.Count == 0)
                return;

            using (Py.GIL())
            {
                foreach (string path in options.ExtraSearchPaths)
                    AppendSysPath(path);
            }
        }
    }

    private static void AppendSysPath(string directory)
    {
        using PyObject sysPath = Py.Import("sys").GetAttr("path");
        using PyObject contains = sysPath.InvokeMethod("__contains__", directory.ToPython());
        if (!contains.As<bool>())
            sysPath.InvokeMethod("append", directory.ToPython());
    }
}
