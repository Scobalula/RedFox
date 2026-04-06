using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.OpenGL.Shaders;

/// <summary>
/// Specifies the OpenGL shader profile to target.
/// </summary>
public enum ShaderProfile
{
    /// <summary>
    /// Desktop OpenGL profile.
    /// </summary>
    Desktop,

    /// <summary>
    /// OpenGL ES profile for embedded and mobile systems.
    /// </summary>
    OpenGles
}

/// <summary>
/// Provides helper methods for loading shader source files from disk with the appropriate version header.
/// </summary>
public static class ShaderSource
{
    /// <summary>
    /// Loads a vertex and fragment shader pair from the application's shader directory.
    /// </summary>
    /// <param name="gl">The OpenGL context used to determine the shader profile.</param>
    /// <param name="programName">The shared name of the shader files (without extension).</param>
    /// <returns>A tuple containing the vertex source and fragment source with the appropriate version header prepended.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the vertex or fragment shader file cannot be found.</exception>
    public static (string VertexSource, string FragmentSource) LoadProgram(GL gl, string programName)
    {
        ShaderProfile profile = GetProfile(gl);
        string shaderDirectory = Path.Combine(AppContext.BaseDirectory, "Shaders");

        string vertexPath = Path.Combine(shaderDirectory, $"{programName}.vert.glsl");
        string fragmentPath = Path.Combine(shaderDirectory, $"{programName}.frag.glsl");

        if (!File.Exists(vertexPath))
            throw new FileNotFoundException($"Shader file not found: {vertexPath}", vertexPath);

        if (!File.Exists(fragmentPath))
            throw new FileNotFoundException($"Shader file not found: {fragmentPath}", fragmentPath);

        return (LoadSource(vertexPath, profile), LoadSource(fragmentPath, profile));
    }

    /// <summary>
    /// Loads a compute shader from the application's shader directory.
    /// </summary>
    /// <param name="programName">The name of the shader file (without extension).</param>
    /// <returns>The compute shader source with the appropriate version header prepended.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the compute shader file cannot be found.</exception>
    public static string LoadCompute(string programName)
    {
        string shaderDirectory = Path.Combine(AppContext.BaseDirectory, "Shaders");
        string computePath = Path.Combine(shaderDirectory, $"{programName}.comp.glsl");

        if (!File.Exists(computePath))
            throw new FileNotFoundException($"Shader file not found: {computePath}", computePath);

        return "#version 430 core\n" + File.ReadAllText(computePath);
    }

    /// <summary>
    /// Determines the appropriate shader profile based on the current OpenGL context version string.
    /// </summary>
    /// <param name="gl">The OpenGL context to inspect.</param>
    /// <returns><see cref="ShaderProfile.OpenGles"/> if the context is OpenGL ES; otherwise, <see cref="ShaderProfile.Desktop"/>.</returns>
    public static ShaderProfile GetProfile(GL gl)
    {
        string version = gl.GetStringS(StringName.Version) ?? string.Empty;
        return version.Contains("OpenGL ES", StringComparison.OrdinalIgnoreCase)
            ? ShaderProfile.OpenGles
            : ShaderProfile.Desktop;
    }

    private static string LoadSource(string filePath, ShaderProfile profile)
    {
        string header = profile == ShaderProfile.OpenGles
            ? "#version 300 es\nprecision highp float;\nprecision highp int;\n"
            : "#version 330 core\n";

        return header + File.ReadAllText(filePath);
    }
}
