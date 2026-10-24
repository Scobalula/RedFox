using System;
using System.IO;
using System.Text.RegularExpressions;

namespace RedFox.Graphics3D.OpenGL.Shaders;

/// <summary>
/// Loads GLSL shader source files from the build output directory.
/// </summary>
public static partial class GlslShaderSourceLoader
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

        string shaderRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Shaders", "Glsl"));
        string absolutePath = Path.Combine(shaderRoot, typeName, fileName);
        if (!File.Exists(absolutePath))
        {
            throw new FileNotFoundException($"GLSL shader file was not found: {absolutePath}", absolutePath);
        }

        return IncludeDirective().Replace(File.ReadAllText(absolutePath), match => File.ReadAllText(Path.Combine(shaderRoot, match.Groups[1].Value)));
    }

    [GeneratedRegex(@"^#include ""([^""]+)""[ \t\r]*$", RegexOptions.Multiline)]
    private static partial Regex IncludeDirective();
}