namespace RedFox.Graphics3D.Formats.KaydaraFbx;

/// <summary>
/// Configuration options that control how a <see cref="Scene"/> is written to an FBX file.
/// </summary>
public class FbxWriteOptions
{
    /// <summary>
    /// Gets or sets the FBX version written to the file. Version 7400 is read by the widest range of applications, while 7500 and later support files larger than 4 GB.
    /// </summary>
    public int Version { get; set; } = 7400;

    /// <summary>
    /// Gets or sets the encoding used when the target file name does not request ASCII with an <c>.ascii.fbx</c> suffix.
    /// </summary>
    public FbxFormat Format { get; set; } = FbxFormat.Binary;

    /// <summary>
    /// Gets or sets a value indicating whether skeleton and morph animations are written.
    /// </summary>
    public bool WriteAnimations { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether materials and textures are written.
    /// </summary>
    public bool WriteMaterials { get; set; } = true;
}
