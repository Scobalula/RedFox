namespace RedFox.Graphics3D.Formats.MayaAscii;

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
