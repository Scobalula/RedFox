using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.OpenGL.Shaders;

public enum ShaderProfile
{
    Desktop,
    OpenGles
}

public static class ShaderSource
{
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
