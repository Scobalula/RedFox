using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RedFox.Plugins.Python;

/// <summary>
/// Discovers the Python shared library on the host system so callers do not need to set
/// <c>PYTHONNET_PYDLL</c> manually. Resolution is best-effort: the first viable path wins
/// and <see langword="null"/> is returned when no candidate is found.
/// </summary>
/// <remarks>
/// <para>The probe runs once per process and caches its result. Pass <c>refresh: true</c> to force a re-scan
/// (for example after the user installs Python while the application is running).</para>
/// <para>Resolution order:</para>
/// <list type="number">
/// <item><c>PYTHONNET_PYDLL</c> environment variable.</item>
/// <item>The Python launcher (<c>py -3</c>) on Windows, or <c>python3</c>/<c>python</c> on other platforms,
/// queried via <c>sys.base_prefix</c> + <c>sysconfig</c>.</item>
/// <item>Common install locations (per-user, machine-wide, framework installs).</item>
/// </list>
/// </remarks>
public static class PythonResolver
{
    private static readonly object _gate = new();
    private static string? _cached;
    private static bool _hasResolved;

    /// <summary>
    /// Returns the cached resolved Python shared library path, running discovery once if necessary.
    /// </summary>
    /// <param name="refresh">When <see langword="true"/> the cache is discarded and a fresh probe runs.</param>
    /// <returns>The absolute path to a Python shared library, or <see langword="null"/> when none was found.</returns>
    public static string? Resolve(bool refresh = false)
    {
        lock (_gate)
        {
            if (_hasResolved && !refresh)
                return _cached;

            _cached = Probe();
            _hasResolved = true;
            return _cached;
        }
    }

    private static string? Probe()
    {
        string? env = Environment.GetEnvironmentVariable("PYTHONNET_PYDLL");
        if (!string.IsNullOrWhiteSpace(env) && File.Exists(env))
            return env;

        string? viaLauncher = ProbeViaLauncher();
        if (viaLauncher is not null)
            return viaLauncher;

        return ProbeCommonLocations();
    }

    private static string? ProbeViaLauncher()
    {
        string[] launchers = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? ["py", "python", "python3"]
            : ["python3", "python"];

        foreach (string launcher in launchers)
        {
            string? path = ProbeViaPythonExecutable(launcher);
            if (path is not null)
                return path;
        }

        return null;
    }

    private static string? ProbeViaPythonExecutable(string executable)
    {
        // Print three lines: base_prefix, LIBDIR (posix) / DLLs dir (windows), LDLIBRARY/python dll name.
        const string script = "import sys, sysconfig\n" +
                              "print(sys.base_prefix)\n" +
                              "print(sysconfig.get_config_var('LIBDIR') or '')\n" +
                              "print(sysconfig.get_config_var('LDLIBRARY') or '')\n" +
                              $"print(f'{{sys.version_info.major}}.{{sys.version_info.minor}}')\n";

        if (!TryRun(executable, ["-c", script], out string? stdout))
            return null;

        string[] lines = stdout!.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 4)
            return null;

        string basePrefix = lines[0].Trim();
        string libDir = lines[1].Trim();
        string ldLibrary = lines[2].Trim();
        string version = lines[3].Trim();
        string compact = version.Replace(".", string.Empty, StringComparison.Ordinal);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return ProbeWindowsDll(basePrefix, compact);

        return ProbeUnixDll(basePrefix, libDir, ldLibrary, version);
    }

    private static string? ProbeWindowsDll(string basePrefix, string compactVersion)
    {
        if (string.IsNullOrEmpty(basePrefix) || string.IsNullOrEmpty(compactVersion))
            return null;

        string candidate = Path.Combine(basePrefix, $"python{compactVersion}.dll");
        return File.Exists(candidate) ? candidate : null;
    }

    private static string? ProbeUnixDll(string basePrefix, string libDir, string ldLibrary, string version)
    {
        if (!string.IsNullOrEmpty(libDir) && !string.IsNullOrEmpty(ldLibrary))
        {
            string direct = Path.Combine(libDir, ldLibrary);
            if (File.Exists(direct))
                return direct;
        }

        if (string.IsNullOrEmpty(version))
            return null;

        string soname = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? $"libpython{version}.dylib"
            : $"libpython{version}.so.1.0";

        foreach (string dir in EnumerateUnixLibDirs(basePrefix, libDir))
        {
            string candidate = Path.Combine(dir, soname);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static IEnumerable<string> EnumerateUnixLibDirs(string basePrefix, string libDir)
    {
        if (!string.IsNullOrEmpty(libDir))
            yield return libDir;

        if (!string.IsNullOrEmpty(basePrefix))
        {
            yield return Path.Combine(basePrefix, "lib");
            yield return Path.Combine(basePrefix, "lib64");
        }

        yield return "/usr/lib";
        yield return "/usr/lib64";
        yield return "/usr/local/lib";
    }

    private static string? ProbeCommonLocations()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return null;

        string localApps = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        string[] roots =
        [
            Path.Combine(localApps, "Programs", "Python"),
            programFiles,
            programFilesX86,
            @"C:\",
        ];

        // Newer versions first.
        for (int minor = 13; minor >= 8; minor--)
        {
            string compact = $"3{minor}";
            string folder = $"Python3{minor}";

            foreach (string root in roots)
            {
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                    continue;

                string candidate = Path.Combine(root, folder, $"python{compact}.dll");
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        return null;
    }

    private static bool TryRun(string fileName, string[] args, out string? stdout)
    {
        stdout = null;
        try
        {
            ProcessStartInfo info = new(fileName)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (string arg in args)
                info.ArgumentList.Add(arg);

            using Process process = Process.Start(info) ?? throw new InvalidOperationException();
            stdout = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(5000))
            {
                try { process.Kill(true); } catch { }
                return false;
            }
            return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(stdout);
        }
        catch
        {
            return false;
        }
    }
}
