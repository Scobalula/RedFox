namespace RedFox.Graphics3D;

/// <summary>
/// An <see cref="Animation"/> that drives <see cref="Morph"/> target weights over time.
/// Each <see cref="MorphAnimationTrack"/> animates the weight of the morph target that shares its name.
/// Pair with <see cref="MorphAnimationSampler"/> for playback.
/// </summary>
public class MorphAnimation : Animation
{
    /// <summary>
    /// Gets or sets the per-target weight animation tracks.
    /// </summary>
    public List<MorphAnimationTrack> Tracks { get; set; } = [];

    /// <summary>
    /// Initializes a new <see cref="MorphAnimation"/> with the given name and a default framerate of 30 fps.
    /// </summary>
    /// <param name="name">The animation name.</param>
    public MorphAnimation(string name) : base(name)
    {
        Framerate = 30;
    }

    /// <summary>
    /// Creates or retrieves the <see cref="MorphAnimationTrack"/> for the given target name.
    /// </summary>
    /// <param name="targetName">Name of the morph target to animate.</param>
    /// <returns>The existing or newly created track.</returns>
    public MorphAnimationTrack GetOrCreateTrack(string targetName)
    {
        MorphAnimationTrack? existing = Tracks.Find(track => track.Name.Equals(targetName, StringComparison.Ordinal));

        if (existing is not null)
            return existing;

        MorphAnimationTrack created = new(targetName);
        Tracks.Add(created);
        return created;
    }

    /// <summary>
    /// Computes the minimum and maximum keyframe times across all tracks.
    /// </summary>
    /// <returns>
    /// A tuple of (minFrame, maxFrame). Returns (float.MaxValue, float.MinValue) if no tracks have keyframes.
    /// </returns>
    public override (float, float) GetAnimationFrameRange()
    {
        float min = float.MaxValue;
        float max = float.MinValue;

        foreach (MorphAnimationTrack track in Tracks)
        {
            if (track.WeightCurve is not { KeyFrameCount: > 0 } curve)
                continue;

            min = MathF.Min(min, curve.StartTime);
            max = MathF.Max(max, curve.EndTime);
        }

        return (min, max);
    }
}
