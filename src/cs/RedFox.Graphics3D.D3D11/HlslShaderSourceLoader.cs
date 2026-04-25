using System;
using System.IO;

namespace RedFox.Graphics3D.D3D11;

internal static class HlslShaderSourceLoader
{
    public static string LoadVertexSource(string materialName)
    {
        return File.ReadAllText(GetVertexSourcePath(materialName));
    }

    public static string LoadFragmentSource(string materialName)
    {
        return File.ReadAllText(GetFragmentSourcePath(materialName));
    }

    public static string LoadComputeSource(string materialName)
    {
        return File.ReadAllText(GetComputeSourcePath(materialName));
    }

    public static string GetVertexSourcePath(string materialName)
    {
        return GetPath(materialName, "Vertex.hlsl");
    }

    public static string GetFragmentSourcePath(string materialName)
    {
        return GetPath(materialName, "Fragment.hlsl");
    }

    public static string GetComputeSourcePath(string materialName)
    {
        return GetPath(materialName, "Compute.hlsl");
    }

    private static string GetPath(string materialName, string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(materialName);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        string baseDirectory = AppContext.BaseDirectory;
        string path = Path.Combine(baseDirectory, "Shaders", "Hlsl", materialName, fileName);
        if (!File.Exists(path))
        {
            path = Path.Combine("Shaders", "Hlsl", materialName, fileName);
        }

        return path;
    }
}
