using System;
using System.IO;

namespace RedFox.Graphics3D.OpenGL.Shaders;

/// <summary>
/// Loads GLSL shader source files from the build output directory.
/// </summary>
public static class GlslShaderSourceLoader
{
    /// <summary>
    /// Loads a vertex shader source for the supplied material type.
    /// </summary>
    /// <param name="typeName">The material type name.</param>
    /// <returns>The GLSL source text.</returns>
    public static string LoadVertexSource(string typeName)
    {
        return LoadShaderSource(typeName, "Vertex.vert");
    }

    /// <summary>
    /// Loads a fragment shader source for the supplied material type.
    /// </summary>
    /// <param name="typeName">The material type name.</param>
    /// <returns>The GLSL source text.</returns>
    public static string LoadFragmentSource(string typeName)
    {
        return LoadShaderSource(typeName, "Fragment.frag");
    }

    /// <summary>
    /// Loads a compute shader source for the supplied material type.
    /// </summary>
    /// <param name="typeName">The material type name.</param>
    /// <returns>The GLSL source text.</returns>
    public static string LoadComputeSource(string typeName)
    {
        return LoadShaderSource(typeName, "Compute.comp");
    }

    private static string LoadShaderSource(string typeName, string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        string shaderDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Shaders", "Glsl", typeName));
        string absolutePath = Path.Combine(shaderDirectory, fileName);
        if (!File.Exists(absolutePath))
        {
            throw new FileNotFoundException($"GLSL shader file was not found: {absolutePath}", absolutePath);
        }

        return File.ReadAllText(absolutePath);
    }
}