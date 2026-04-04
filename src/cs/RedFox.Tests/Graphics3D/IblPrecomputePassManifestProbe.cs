using System.Reflection;
using RedFox.Graphics3D.OpenGL;
using RedFox.Graphics3D.OpenGL.Passes;

namespace RedFox.Tests.Graphics3D;

internal static class IblPrecomputePassManifestProbe
{
    private static readonly MethodInfo ManifestMatchesMethod =
        typeof(IblPrecomputePass).GetMethod("ManifestMatches", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("Could not find IblPrecomputePass.ManifestMatches.");

    public static bool ManifestMatches(EnvironmentCacheManifest loaded, EnvironmentCacheManifest expected)
    {
        return (bool)(ManifestMatchesMethod.Invoke(null, [loaded, expected]) ?? false);
    }
}
