using System.Reflection;

namespace ToolName;

/// <summary>
/// Exposes the version and build type stamped onto the entry assembly by the build.
/// </summary>
public static class ToolNameBuildInfo
{
    /// <summary>
    /// Gets the informational version with the build type appended, such as <c>2026.10.5.3 (Release)</c>.
    /// </summary>
    public static string DisplayVersion
    {
        get
        {
            Assembly assembly = Assembly.GetEntryAssembly() ?? typeof(ToolNameBuildInfo).Assembly;
            string version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? assembly.GetName().Version?.ToString(4) ?? "unknown";
            string? buildType = assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(attribute => attribute.Key == "BuildType")?.Value;

            return string.IsNullOrWhiteSpace(buildType) ? version : $"{version} ({buildType})";
        }
    }
}
