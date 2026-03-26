namespace RedFox.Graphics3D.MayaAscii;

/// <summary>
/// Specifies the Maya scene unit used in the <c>currentUnit</c> header of a Maya ASCII file.
/// The unit affects how Maya interprets distance values throughout the scene.
/// </summary>
public enum MayaLinearUnit
{
    /// <summary>
    /// Millimeters (mm). One thousandth of a meter.
    /// </summary>
    Millimeter,

    /// <summary>
    /// Centimeters (cm). The default unit for most Maya scenes.
    /// </summary>
    Centimeter,

    /// <summary>
    /// Meters (m). SI base unit of length.
    /// </summary>
    Meter,

    /// <summary>
    /// Inches (in). Imperial unit of length.
    /// </summary>
    Inch,

    /// <summary>
    /// Feet (ft). Imperial unit equal to 12 inches.
    /// </summary>
    Foot,

    /// <summary>
    /// Yards (yd). Imperial unit equal to 3 feet.
    /// </summary>
    Yard,
}

/// <summary>
/// Specifies the angular unit used in the <c>currentUnit</c> header of a Maya ASCII file.
/// Determines how Maya interprets rotation and angle attribute values.
/// </summary>
public enum MayaAngularUnit
{
    /// <summary>
    /// Degrees (deg). The default angular unit in most Maya scenes.
    /// </summary>
    Degree,

    /// <summary>
    /// Radians (rad). The SI unit of angular measurement.
    /// </summary>
    Radian,
}

/// <summary>
/// Specifies the time unit (framerate) used in the <c>currentUnit</c> header of a Maya ASCII file.
/// Controls the time base for animation playback and keyframe evaluation.
/// </summary>
public enum MayaTimeUnit
{
    /// <summary>
    /// Film rate: 24 frames per second.
    /// </summary>
    Film,

    /// <summary>
    /// Game rate: 15 frames per second.
    /// </summary>
    Game,

    /// <summary>
    /// NTSC rate: approximately 29.97 frames per second.
    /// </summary>
    Ntsc,

    /// <summary>
    /// PAL rate: 25 frames per second.
    /// </summary>
    Pal,

    /// <summary>
    /// Show rate: 48 frames per second (double film).
    /// </summary>
    Show,

    /// <summary>
    /// NTSC field rate: approximately 59.94 fields per second.
    /// </summary>
    NtscField,

    /// <summary>
    /// PAL field rate: 50 fields per second.
    /// </summary>
    PalField,
}

/// <summary>
/// Specifies the up axis orientation written to the Maya ASCII file header.
/// Determines which world axis is considered "up" in the Maya scene.
/// </summary>
public enum MayaUpAxis
{
    /// <summary>
    /// The Y axis is the up direction. This is the Maya default.
    /// </summary>
    Y,

    /// <summary>
    /// The Z axis is the up direction. Common in architectural and some game pipelines.
    /// </summary>
    Z,
}
