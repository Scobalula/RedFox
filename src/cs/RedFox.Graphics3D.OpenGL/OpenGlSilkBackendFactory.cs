using RedFox.Graphics3D.Silk;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace RedFox.Graphics3D.OpenGL;

/// <summary>
/// Creates OpenGL renderer backends for the shared Silk renderer host.
/// </summary>
public sealed class OpenGlSilkBackendFactory : ISilkRendererBackendFactory
{
    private const int RequiredOpenGlMajorVersion = 4;
    private const int RequiredOpenGlMinorVersion = 3;

    /// <summary>
    /// Configures the Silk window for an OpenGL context.
    /// </summary>
    /// <param name="options">The window options to configure.</param>
    public void ConfigureWindowOptions(ref WindowOptions options)
    {
        options.API = new GraphicsAPI(
            ContextAPI.OpenGL,
            ContextProfile.Core,
            ContextFlags.ForwardCompatible,
            new APIVersion(RequiredOpenGlMajorVersion, RequiredOpenGlMinorVersion));
    }

    /// <summary>
    /// Creates an OpenGL renderer backend for the loaded window.
    /// </summary>
    /// <param name="window">The loaded Silk window.</param>
    /// <returns>The created renderer backend.</returns>
    public ISilkRendererBackend CreateBackend(IWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);
        GL gl = GL.GetApi(window);
        ValidateOpenGlVersion(gl);
        Console.WriteLine($"[OpenGL] Context: {RequiredOpenGlMajorVersion}.{RequiredOpenGlMinorVersion}+ ready.");
        return new OpenGlSilkBackend(new OpenGlGraphicsDevice(gl));
    }

    private static void ValidateOpenGlVersion(GL gl)
    {
        int majorVersion = 0;
        int minorVersion = 0;
        gl.GetInteger(GLEnum.MajorVersion, out majorVersion);
        gl.GetInteger(GLEnum.MinorVersion, out minorVersion);
        if (majorVersion < RequiredOpenGlMajorVersion || (majorVersion == RequiredOpenGlMajorVersion && minorVersion < RequiredOpenGlMinorVersion))
        {
            throw new InvalidOperationException($"OpenGL {RequiredOpenGlMajorVersion}.{RequiredOpenGlMinorVersion}+ is required, but the active context is {majorVersion}.{minorVersion}.");
        }
    }
}
