namespace RedFox.Graphics3D.OpenGL;

/// <summary>
/// Controls how the environment map is flipped vertically during loading.
/// </summary>
public enum EnvironmentMapFlipMode
{
    /// <summary>Automatically determine flip behaviour from the file format.</summary>
    Auto = 0,

    /// <summary>Always flip the environment map vertically.</summary>
    ForceFlipY = 1,

    /// <summary>Never flip the environment map vertically.</summary>
    ForceNoFlipY = 2,
}
