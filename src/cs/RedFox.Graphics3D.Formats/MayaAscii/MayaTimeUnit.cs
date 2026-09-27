namespace RedFox.Graphics3D.Formats.MayaAscii;

/// <summary>
/// Specifies the time unit in a Maya ASCII file.
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
    /// Show rate: 48 frames per second.
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
