namespace RedFox.Graphics3D;

/// <summary>
/// Controls whether combined models share matching bones.
/// </summary>
public enum SceneCombineMode
{
    /// <summary>
    /// Keeps models at their original placement with independent skeletons.
    /// </summary>
    InPlace,

    /// <summary>
    /// Shares same-named bones, retaining the earliest model's hierarchy and bind pose.
    /// </summary>
    ConnectBones,
}
