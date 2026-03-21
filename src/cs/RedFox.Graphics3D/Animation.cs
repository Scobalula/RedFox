using System.Numerics;

namespace RedFox.Graphics3D;

/// <summary>
/// Abstract base class for all animation types in the RedFox scene graph.
/// Provides common metadata (name, framerate, actions) and helper methods
/// for keyframe lookup. Concrete subclasses (e.g. <c>SkeletonAnimation</c>,
/// <c>BlendShapeAnimation</c>) define their own track storage.
/// <para>
/// Extends <see cref="SceneNode"/> so animations participate in the scene
/// hierarchy and can be discovered via standard traversal.
/// </para>
/// </summary>
public abstract class Animation(string name) : SceneNode(name)
{
    /// <summary>
    /// Gets or sets the animation actions — callbacks triggered at specific
    /// keyframe times during playback (e.g. sound cues, particle spawns).
    /// </summary>
    public List<AnimationAction>? Actions { get; set; }

    /// <summary>
    /// Gets or sets the playback framerate in frames per second.
    /// Used to convert between frame indices and wall-clock time.
    /// </summary>
    public float Framerate { get; set; }

    /// <summary>
    /// Computes the minimum and maximum keyframe times across all tracks
    /// in this animation. Subclasses implement this by iterating their
    /// concrete track storage.
    /// </summary>
    /// <returns>A tuple of (minFrame, maxFrame).</returns>
    public abstract (float, float) GetAnimationFrameRange();

    /// <summary>
    /// Calculates the total number of action keyframes across all actions.
    /// </summary>
    /// <returns>The cumulative keyframe count for all actions.</returns>
    public int GetAnimationActionCount() =>
        Actions?.Sum(x => x.KeyFrames.Count) ?? 0;

    /// <summary>
    /// Creates or retrieves a named <see cref="AnimationAction"/>.
    /// If an action with the same name already exists, it is returned unchanged.
    /// </summary>
    /// <param name="actionName">Name of the action.</param>
    /// <returns>The new or existing action.</returns>
    public AnimationAction CreateAction(string actionName)
    {
        Actions ??= [];
        var idx = Actions.FindIndex(x => x.Name == actionName);
        if (idx != -1) return Actions[idx];

        var action = new AnimationAction(actionName, "Default");
        Actions.Add(action);
        return action;
    }

    /// <summary>
    /// Creates or retrieves a named <see cref="AnimationAction"/> and populates
    /// it with the provided keyframes if it is newly created.
    /// </summary>
    /// <param name="actionName">Name of the action.</param>
    /// <param name="keyFrames">Keyframes to add if the action is new.</param>
    /// <returns>The new or existing action.</returns>
    public AnimationAction CreateAction(string actionName, IEnumerable<AnimationKeyFrame<float, Action<Scene>?>> keyFrames)
    {
        Actions ??= [];
        var idx = Actions.FindIndex(x => x.Name == actionName);
        if (idx != -1) return Actions[idx];

        var action = new AnimationAction(actionName, "Default");
        action.KeyFrames.AddRange(keyFrames);
        Actions.Add(action);
        return action;
    }

    // ------------------------------------------------------------------
    // Legacy helpers for List<AnimationKeyFrame> (used by AnimationAction)
    // ------------------------------------------------------------------

    /// <summary>
    /// Gets the previous and next frame pair indices at the given time from a
    /// sorted list of keyframes. Used by <see cref="AnimationAction"/> and
    /// weight curves that still use <see cref="AnimationKeyFrame{TFrame,TValue}"/>.
    /// </summary>
    /// <typeparam name="TFrame">Numeric frame type.</typeparam>
    /// <typeparam name="TValue">Value type stored at each keyframe.</typeparam>
    /// <param name="list">Sorted keyframe list to search.</param>
    /// <param name="time">Current playback time.</param>
    /// <param name="startTime">Offset added to each keyframe's time.</param>
    /// <param name="cursor">Hint index for sequential scanning.</param>
    /// <returns>Indices of the surrounding keyframes, or (-1, -1) if empty.</returns>
    public static (int, int) GetFramePairIndex<TFrame, TValue>(
        List<AnimationKeyFrame<TFrame, TValue>>? list,
        TFrame time,
        TFrame startTime,
        int cursor = 0)
        where TFrame : INumber<TFrame>
    {
        if (list is null || list.Count == 0) return (-1, -1);
        if (list.Count == 1) return (0, 0);
        if (time > startTime + list[^1].Frame) return (list.Count - 1, list.Count - 1);
        if (time < startTime + list[0].Frame) return (0, 0);

        // First pass from cursor
        for (var i = cursor; i < list.Count - 1; i++)
            if (time < startTime + list[i + 1].Frame)
                return (i, i + 1);
        // Second pass up to cursor
        for (var i = 0; i < list.Count - 1 && i < cursor; i++)
            if (time < startTime + list[i + 1].Frame)
                return (i, i + 1);

        return (list.Count - 1, list.Count - 1);
    }

    /// <summary>
    /// Enumerates keyframes from a nullable collection, yielding nothing if
    /// the collection is <see langword="null"/>.
    /// </summary>
    public static IEnumerable<AnimationKeyFrame<TFrame, TValue>> EnumerateKeyFrames<TFrame, TValue>(
        IEnumerable<AnimationKeyFrame<TFrame, TValue>>? keyFrames)
        where TFrame : INumber<TFrame>
    {
        if (keyFrames is null) yield break;
        foreach (var kf in keyFrames)
            yield return kf;
    }
}
