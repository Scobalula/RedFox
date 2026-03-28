namespace RedFox.Graphics3D.MayaAscii;

/// <summary>
/// Configuration options that control how a <see cref="Scene"/> is written to a Maya ASCII (.ma) file.
/// These options affect the file header, unit system, and data included in the output.
/// </summary>
public class MayaAsciiWriteOptions
{
    /// <summary>
    /// Gets or sets the linear (distance) unit written to the Maya ASCII file header.
    /// Maya uses this to interpret all distance-based attribute values in the scene.
    /// </summary>
    public MayaLinearUnit LinearUnit { get; set; } = MayaLinearUnit.Centimeter;

    /// <summary>
    /// Gets or sets the angular unit written to the Maya ASCII file header.
    /// Maya uses this to interpret all angle-based attribute values in the scene.
    /// </summary>
    public MayaAngularUnit AngularUnit { get; set; } = MayaAngularUnit.Degree;

    /// <summary>
    /// Gets or sets the time unit (framerate) written to the Maya ASCII file header.
    /// Maya uses this as the base time unit for animation playback and keyframing.
    /// </summary>
    public MayaTimeUnit TimeUnit { get; set; } = MayaTimeUnit.Film;

    /// <summary>
    /// Gets or sets the up axis orientation written to the Maya ASCII file header.
    /// Determines which world axis Maya treats as "up" for the entire scene.
    /// The RedFox scene graph uses Z-up by convention (FBX import strips Y-up basis
    /// containers), so the default is <see cref="MayaUpAxis.Z"/>.
    /// </summary>
    public MayaUpAxis UpAxis { get; set; } = MayaUpAxis.Z;

    /// <summary>
    /// Gets or sets a value indicating whether skeleton animation tracks should be exported.
    /// When set to <see langword="false"/>, only static geometry and skeleton data will be written.
    /// </summary>
    public bool WriteAnimations { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether material and shader nodes should be exported.
    /// When set to <see langword="false"/>, geometry is written without shading group assignments.
    /// </summary>
    public bool WriteMaterials { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether vertex normals should be included in the mesh output.
    /// When set to <see langword="false"/>, Maya will compute normals from the polygon topology.
    /// </summary>
    public bool WriteNormals { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether vertex color layers should be included in the mesh output.
    /// When set to <see langword="false"/>, color set data is omitted from the file.
    /// </summary>
    public bool WriteVertexColors { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether raw vertex positions should be written
    /// without applying bind-pose inverse transforms. When <see langword="true"/>, positions
    /// are written as-is from the mesh data buffers.
    /// </summary>
    public bool WriteRawVertices { get; set; }
}
