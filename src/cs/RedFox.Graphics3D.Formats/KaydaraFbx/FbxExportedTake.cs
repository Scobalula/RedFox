namespace RedFox.Graphics3D.Formats.KaydaraFbx;

/// <summary>
/// Describes an exported animation take written to the FBX <c>Takes</c> section.
/// </summary>
/// <param name="Name">The take name, matching its animation stack.</param>
/// <param name="Start">The start time in FBX ticks.</param>
/// <param name="Stop">The stop time in FBX ticks.</param>
public readonly record struct FbxExportedTake(string Name, long Start, long Stop);
