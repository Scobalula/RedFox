namespace RedFox.Graphics3D.Formats.KaydaraFbx;

/// <summary>
/// Describes how an FBX animation curve continues before its first key or after its last key.
/// </summary>
/// <param name="Type">The extrapolation type: <c>C</c> constant, <c>R</c> repeat, <c>M</c> mirror, <c>A</c> repeat with offset or <c>K</c> keep slope.</param>
/// <param name="Repetition">The number of repetitions, or a negative value for unlimited repetitions.</param>
public readonly record struct FbxCurveExtrapolation(char Type, int Repetition)
{
    /// <summary>
    /// Gets the default constant extrapolation.
    /// </summary>
    public static FbxCurveExtrapolation Constant => new('C', -1);

    /// <summary>
    /// Reads a <c>Pre-Extrapolation</c> or <c>Post-Extrapolation</c> node.
    /// </summary>
    /// <param name="node">The extrapolation node, or <see langword="null"/>.</param>
    /// <returns>The extrapolation, or <see cref="Constant"/> when the node is missing.</returns>
    public static FbxCurveExtrapolation Read(FbxNode? node)
    {
        if (node is null || !node.TryGetChildProperty("Type", out FbxProperty type))
            return Constant;

        char	mode		= type.Type == FbxPropertyType.String ? type.AsString().FirstOrDefault('C') : (char)type.AsInt64();
        int		repetition	= node.TryGetChildProperty("Repetition", out FbxProperty count) && count.IsNumber ? count.AsInt32() : -1;

        return new FbxCurveExtrapolation(char.ToUpperInvariant(mode), repetition);
    }
}
