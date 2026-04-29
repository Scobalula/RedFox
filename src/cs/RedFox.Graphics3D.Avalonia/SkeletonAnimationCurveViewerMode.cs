namespace RedFox.Graphics3D.Avalonia;

/// <summary>
/// Selects which region of a skeleton animation curve viewer is displayed.
/// </summary>
public enum SkeletonAnimationCurveViewerMode
{
    /// <summary>
    /// Shows the curve list, graph, and key list.
    /// </summary>
    Full = 0,

    /// <summary>
    /// Shows only the curve list region.
    /// </summary>
    CurveList = 1,

    /// <summary>
    /// Shows only the graph and key list region.
    /// </summary>
    Graph = 2,
}