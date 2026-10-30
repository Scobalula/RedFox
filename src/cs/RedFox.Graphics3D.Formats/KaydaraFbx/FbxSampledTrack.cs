using System.Numerics;

namespace RedFox.Graphics3D.Formats.KaydaraFbx;

/// <summary>
/// Holds the per-frame local transform samples of an exported animation track.
/// </summary>
/// <param name="node">The animated scene node.</param>
/// <param name="frameCount">The number of sampled frames.</param>
public sealed class FbxSampledTrack(SceneNode node, int frameCount)
{
    /// <summary>
    /// Gets the animated scene node.
    /// </summary>
    public SceneNode Node { get; } = node;

    /// <summary>
    /// Gets the sampled local translations.
    /// </summary>
    public Vector3[] Translations { get; } = new Vector3[frameCount];

    /// <summary>
    /// Gets the sampled local rotations.
    /// </summary>
    public Quaternion[] Rotations { get; } = new Quaternion[frameCount];

    /// <summary>
    /// Gets the sampled local scales.
    /// </summary>
    public Vector3[] Scales { get; } = new Vector3[frameCount];

    /// <summary>
    /// Stores the local transform sampled at a frame.
    /// </summary>
    /// <param name="index">The zero-based frame index.</param>
    /// <param name="translation">The local translation.</param>
    /// <param name="rotation">The local rotation.</param>
    /// <param name="scale">The local scale.</param>
    public void Set(int index, Vector3 translation, Quaternion rotation, Vector3 scale)
    {
        Translations[index]	= translation;
        Rotations[index]	= rotation;
        Scales[index]		= scale;
    }

    /// <summary>
    /// Converts the sampled rotations to continuous XYZ Euler angles in degrees.
    /// </summary>
    /// <returns>The Euler angles, unwrapped so consecutive frames do not flip.</returns>
    public Vector3[] GetEulerRotations()
    {
        Vector3[] angles = new Vector3[Rotations.Length];

        for (int i = 0; i < angles.Length; i++)
            angles[i] = i == 0 ? FbxTransform.QuaternionToEulerXyz(Rotations[i]) : FbxTransform.QuaternionToEulerXyz(Rotations[i], angles[i - 1]);

        return angles;
    }
}
