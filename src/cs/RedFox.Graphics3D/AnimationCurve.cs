using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using RedFox.Graphics3D.Buffers;

namespace RedFox.Graphics3D;

/// <summary>
/// Represents an animation curve backed by <see cref="DataBuffer"/> storage,
/// mirroring the buffer-based approach used by <see cref="Mesh"/>.
/// <para>
/// Keys (times) and values are stored in separate <see cref="DataBuffer"/>
/// instances, where the value buffer's <see cref="DataBuffer.ComponentCount"/>
/// determines the data dimensionality:
/// <list type="bullet">
///   <item><description>1 — scalar <see cref="float"/></description></item>
///   <item><description>3 — <see cref="Vector3"/> (translation / scale)</description></item>
///   <item><description>4 — <see cref="Quaternion"/> (rotation)</description></item>
/// </list>
/// </para>
/// </summary>
[DebuggerDisplay("KeyFrameCount = {KeyFrameCount}, Components = {ComponentCount}")]
public class AnimationCurve
{
    /// <summary>
    /// Gets or sets the buffer containing keyframe times.
    /// Each element is a single float representing the time of that keyframe.
    /// Layout: <c>ElementCount = keyframe count, ValueCount = 1, ComponentCount = 1</c>.
    /// </summary>
    public DataBuffer? Keys { get; set; }

    /// <summary>
    /// Gets or sets the buffer containing keyframe values.
    /// The <see cref="DataBuffer.ComponentCount"/> determines the value type
    /// (1 = scalar, 3 = Vector3, 4 = Quaternion).
    /// Layout: <c>ElementCount = keyframe count, ValueCount = 1, ComponentCount = N</c>.
    /// </summary>
    public DataBuffer? Values { get; set; }

    /// <summary>
    /// Gets or sets the coordinate space in which this curve's values are expressed.
    /// </summary>
    public TransformSpace TransformSpace { get; set; }

    /// <summary>
    /// Gets or sets how this curve's values should be interpreted relative to a
    /// base or parent transformation (absolute, relative, additive, etc.).
    /// </summary>
    public TransformType TransformType { get; set; }

    /// <summary>
    /// Gets the number of keyframes stored in this curve.
    /// </summary>
    public int KeyFrameCount => Keys?.ElementCount ?? 0;

    /// <summary>
    /// Gets the number of components per value (1 = scalar, 3 = Vector3, 4 = Quaternion).
    /// Returns 0 if no value buffer has been allocated.
    /// </summary>
    public int ComponentCount => Values?.ComponentCount ?? 0;

    /// <summary>
    /// Gets the time of the first keyframe, or 0 if the curve is empty.
    /// </summary>
    public float StartTime => KeyFrameCount > 0 ? GetKeyTime(0) : 0f;

    /// <summary>
    /// Gets the time of the last keyframe, or 0 if the curve is empty.
    /// </summary>
    public float EndTime => KeyFrameCount > 0 ? GetKeyTime(KeyFrameCount - 1) : 0f;

    /// <summary>
    /// Gets the duration spanned by all keyframes (last time - first time).
    /// Returns 0 if the curve has fewer than 2 keyframes.
    /// </summary>
    public float Duration => KeyFrameCount >= 2 ? EndTime - StartTime : 0f;

    /// <summary>
    /// Initializes a new empty <see cref="AnimationCurve"/> with no buffers allocated.
    /// Buffers are lazily created on the first <c>Add</c> call.
    /// </summary>
    public AnimationCurve() { }

    /// <summary>
    /// Initializes a new <see cref="AnimationCurve"/> with the specified transform
    /// space and type. Buffers are lazily created on the first <c>Add</c> call.
    /// </summary>
    /// <param name="space">The coordinate space for this curve's values.</param>
    /// <param name="type">The transform interpretation mode.</param>
    public AnimationCurve(TransformSpace space, TransformType type)
    {
        TransformSpace = space;
        TransformType = type;
    }

    /// <summary>
    /// Creates a new curve configured for scalar (single-float) values.
    /// </summary>
    /// <returns>A new <see cref="AnimationCurve"/> with 1-component value buffer.</returns>
    public static AnimationCurve CreateScalar() => CreateScalar(capacity: 0);

    /// <summary>
    /// Creates a new curve configured for scalar (single-float) values.
    /// </summary>
    /// <param name="capacity">Initial keyframe capacity (0 for default).</param>
    /// <returns>A new <see cref="AnimationCurve"/> with 1-component value buffer.</returns>
    public static AnimationCurve CreateScalar(int capacity)
    {
        return new AnimationCurve
        {
            Keys = new DataBuffer<float>(capacity, 1, 1),
            Values = new DataBuffer<float>(capacity, 1, 1),
        };
    }

    /// <summary>
    /// Creates a new curve configured for <see cref="Vector3"/> values.
    /// </summary>
    /// <param name="capacity">Initial keyframe capacity (0 for default).</param>
    /// <returns>A new <see cref="AnimationCurve"/> with 3-component value buffer.</returns>
    public static AnimationCurve CreateVector3() => CreateVector3(capacity: 0);

    /// <summary>
    /// Creates a new curve configured for <see cref="Vector3"/> values.
    /// </summary>
    /// <returns>A new <see cref="AnimationCurve"/> with 3-component value buffer.</returns>
    public static AnimationCurve CreateVector3(int capacity)
    {
        return new AnimationCurve
        {
            Keys = new DataBuffer<float>(capacity, 1, 1),
            Values = new DataBuffer<float>(capacity, 1, 3),
        };
    }

    /// <summary>
    /// Creates a new curve configured for <see cref="Quaternion"/> values (rotation).
    /// </summary>
    /// <returns>A new <see cref="AnimationCurve"/> with 4-component value buffer.</returns>
    public static AnimationCurve CreateQuaternion() => CreateQuaternion(capacity: 0);

    /// <summary>
    /// Creates a new curve configured for <see cref="Quaternion"/> values (rotation).
    /// </summary>
    /// <param name="capacity">Initial keyframe capacity (0 for default).</param>
    /// <returns>A new <see cref="AnimationCurve"/> with 4-component value buffer.</returns>
    public static AnimationCurve CreateQuaternion(int capacity)
    {
        return new AnimationCurve
        {
            Keys = new DataBuffer<float>(capacity, 1, 1),
            Values = new DataBuffer<float>(capacity, 1, 4),
        };
    }

    /// <summary>
    /// Creates a new curve configured for <see cref="Vector3"/> values with the
    /// specified transform space and type.
    /// </summary>
    public static AnimationCurve CreateVector3(TransformSpace space, TransformType type) => CreateVector3(space, type, capacity: 0);

    public static AnimationCurve CreateVector3(TransformSpace space, TransformType type, int capacity)
    {
        var curve = CreateVector3(capacity);
        curve.TransformSpace = space;
        curve.TransformType = type;
        return curve;
    }

    /// <summary>
    /// Creates a new curve configured for <see cref="Quaternion"/> values with the
    /// specified transform space and type.
    /// </summary>
    public static AnimationCurve CreateQuaternion(TransformSpace space, TransformType type)
        => CreateQuaternion(space, type, capacity: 0);

    public static AnimationCurve CreateQuaternion(TransformSpace space, TransformType type, int capacity)
    {
        var curve = CreateQuaternion(capacity);
        curve.TransformSpace = space;
        curve.TransformType = type;
        return curve;
    }

    // ------------------------------------------------------------------
    // Add methods — append keyframes to the curve
    // ------------------------------------------------------------------

    /// <summary>
    /// Appends a scalar keyframe at the specified time.
    /// Lazily allocates 1-component buffers if not yet created.
    /// </summary>
    /// <param name="time">The keyframe time.</param>
    /// <param name="value">The scalar value at this keyframe.</param>
    public void Add(float time, float value)
    {
        Keys ??= new DataBuffer<float>(0, 1, 1);
        Values ??= new DataBuffer<float>(0, 1, 1);

        var idx = Keys.ElementCount;
        Keys.Add(idx, 0, 0, time);
        Values.Add(idx, 0, 0, value);
    }

    /// <summary>
    /// Appends a <see cref="Vector3"/> keyframe at the specified time.
    /// Lazily allocates 3-component buffers if not yet created.
    /// </summary>
    /// <param name="time">The keyframe time.</param>
    /// <param name="value">The Vector3 value at this keyframe.</param>
    public void Add(float time, Vector3 value)
    {
        Keys ??= new DataBuffer<float>(0, 1, 1);
        Values ??= new DataBuffer<float>(0, 1, 3);

        var idx = Keys.ElementCount;
        Keys.Add(idx, 0, 0, time);
        Values.Add(idx, 0, 0, value.X);
        Values.Set(idx, 0, 1, value.Y);
        Values.Set(idx, 0, 2, value.Z);
    }

    /// <summary>
    /// Appends a <see cref="Quaternion"/> keyframe at the specified time.
    /// Lazily allocates 4-component buffers if not yet created.
    /// </summary>
    /// <param name="time">The keyframe time.</param>
    /// <param name="value">The Quaternion value at this keyframe.</param>
    public void Add(float time, Quaternion value)
    {
        Keys ??= new DataBuffer<float>(0, 1, 1);
        Values ??= new DataBuffer<float>(0, 1, 4);

        var idx = Keys.ElementCount;
        Keys.Add(idx, 0, 0, time);
        Values.Add(idx, 0, 0, value.X);
        Values.Set(idx, 0, 1, value.Y);
        Values.Set(idx, 0, 2, value.Z);
        Values.Set(idx, 0, 3, value.W);
    }

    // ------------------------------------------------------------------
    // Keyframe access — read individual keyframe data
    // ------------------------------------------------------------------

    /// <summary>
    /// Gets the time of the keyframe at the specified index.
    /// </summary>
    /// <param name="index">Zero-based keyframe index.</param>
    /// <returns>The time value for that keyframe.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetKeyTime(int index) => Keys!.Get<float>(index, 0, 0);

    /// <summary>
    /// Gets the scalar value of the keyframe at the specified index.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetScalar(int index) => Values!.Get<float>(index, 0, 0);

    /// <summary>
    /// Gets the <see cref="Vector3"/> value of the keyframe at the specified index.
    /// Requires the value buffer to have at least 3 components.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector3 GetVector3(int index) => Values!.GetVector3(index, 0);

    /// <summary>
    /// Gets the <see cref="Quaternion"/> value of the keyframe at the specified index.
    /// Requires the value buffer to have at least 4 components.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Quaternion GetQuaternion(int index) => Values!.GetVector4(index, 0).AsQuaternion();

    // ------------------------------------------------------------------
    // Frame pair lookup — binary search for surrounding keyframes
    // ------------------------------------------------------------------

    /// <summary>
    /// Finds the indices of the two keyframes that bracket the given <paramref name="time"/>.
    /// Uses binary search for O(log n) lookup. If the time is outside the curve's
    /// range, the nearest boundary index is returned for both.
    /// </summary>
    /// <param name="time">The time to search for.</param>
    /// <returns>
    /// A tuple of (previousIndex, nextIndex). Returns (-1, -1) if the curve is empty.
    /// Returns (i, i) if only one keyframe exists or the time is at/beyond the boundary.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (int Previous, int Next) GetFramePairIndex(float time)
    {
        if (Keys is null) return (-1, -1);

        var count = Keys.ElementCount;
        if (count == 0) return (-1, -1);
        if (count == 1) return (0, 0);

        var firstTime = GetKeyTime(0);
        var lastTime = GetKeyTime(count - 1);

        if (time <= firstTime) return (0, 0);
        if (time >= lastTime) return (count - 1, count - 1);

        // Binary search for the interval containing 'time'
        int lo = 0, hi = count - 1;
        while (lo < hi - 1)
        {
            var mid = (lo + hi) >>> 1; // unsigned shift avoids overflow
            if (GetKeyTime(mid) <= time)
                lo = mid;
            else
                hi = mid;
        }

        return (lo, hi);
    }

    /// <summary>
    /// Finds the frame pair using a cursor hint for sequential playback.
    /// Starts scanning from <paramref name="cursor"/> for better cache locality
    /// during linear playback, falling back to binary search on miss.
    /// </summary>
    /// <param name="time">The time to search for.</param>
    /// <param name="cursor">Hint index to start scanning from.</param>
    /// <returns>A tuple of (previousIndex, nextIndex) and an updated cursor.</returns>
    public (int Previous, int Next, int Cursor) GetFramePairIndex(float time, int cursor)
    {
        if (Keys is null) return (-1, -1, 0);

        var count = Keys.ElementCount;
        if (count == 0) return (-1, -1, 0);
        if (count == 1) return (0, 0, 0);

        var firstTime = GetKeyTime(0);
        var lastTime = GetKeyTime(count - 1);

        if (time <= firstTime) return (0, 0, 0);
        if (time >= lastTime) return (count - 1, count - 1, count - 1);

        // Try scanning forward from cursor first (common case: sequential playback)
        cursor = Math.Clamp(cursor, 0, count - 2);
        if (GetKeyTime(cursor) <= time && time < GetKeyTime(cursor + 1))
            return (cursor, cursor + 1, cursor);

        // Fall back to binary search
        var (prev, next) = GetFramePairIndex(time);
        return (prev, next, prev);
    }

    // ------------------------------------------------------------------
    // Sampling — interpolate values at arbitrary times
    // ------------------------------------------------------------------

    /// <summary>
    /// Computes the interpolation factor between two keyframes at the given time.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float GetLerpFactor(int prevIndex, int nextIndex, float time)
    {
        if (prevIndex == nextIndex) return 0f;
        var t0 = GetKeyTime(prevIndex);
        var t1 = GetKeyTime(nextIndex);
        var range = t1 - t0;
        return range > 0f ? (time - t0) / range : 0f;
    }

    /// <summary>
    /// Samples the curve at the specified time, returning an interpolated scalar value.
    /// Uses linear interpolation between the two surrounding keyframes.
    /// </summary>
    /// <param name="time">The time to sample at.</param>
    /// <returns>The interpolated scalar value, or 0 if the curve is empty.</returns>
    public float SampleScalar(float time)
    {
        var (i0, i1) = GetFramePairIndex(time);
        if (i0 < 0) return 0f;
        if (i0 == i1) return GetScalar(i0);

        return float.Lerp(GetScalar(i0), GetScalar(i1), GetLerpFactor(i0, i1, time));
    }

    /// <summary>
    /// Samples the curve at the specified time, returning an interpolated
    /// <see cref="Vector3"/> value using linear interpolation.
    /// </summary>
    /// <param name="time">The time to sample at.</param>
    /// <returns>The interpolated Vector3 value, or <see cref="Vector3.Zero"/> if empty.</returns>
    public Vector3 SampleVector3(float time)
    {
        var (i0, i1) = GetFramePairIndex(time);
        if (i0 < 0) return Vector3.Zero;
        if (i0 == i1) return GetVector3(i0);

        return Vector3.Lerp(GetVector3(i0), GetVector3(i1), GetLerpFactor(i0, i1, time));
    }

    /// <summary>
    /// Samples the curve at the specified time, returning an interpolated
    /// <see cref="Quaternion"/> value using spherical linear interpolation (Slerp).
    /// </summary>
    /// <param name="time">The time to sample at.</param>
    /// <returns>The interpolated Quaternion value, or <see cref="Quaternion.Identity"/> if empty.</returns>
    public Quaternion SampleQuaternion(float time)
    {
        var (i0, i1) = GetFramePairIndex(time);
        if (i0 < 0) return Quaternion.Identity;
        if (i0 == i1) return GetQuaternion(i0);

        return Quaternion.Slerp(GetQuaternion(i0), GetQuaternion(i1), GetLerpFactor(i0, i1, time));
    }

    /// <summary>
    /// Samples the curve using a custom extraction and interpolation function.
    /// This is the generic sampling path for custom data types.
    /// </summary>
    /// <typeparam name="T">The value type to extract and interpolate.</typeparam>
    /// <param name="time">The time to sample at.</param>
    /// <param name="extract">Function to extract a value from the buffer at a given keyframe index.</param>
    /// <param name="interpolate">Function to interpolate between two values.</param>
    /// <param name="defaultValue">Value returned when the curve is empty.</param>
    /// <returns>The interpolated value at the given time.</returns>
    public T Sample<T>(float time, Func<AnimationCurve, int, T> extract, Func<T, T, float, T> interpolate, T defaultValue)
    {
        var (i0, i1) = GetFramePairIndex(time);
        if (i0 < 0) return defaultValue;
        if (i0 == i1) return extract(this, i0);

        return interpolate(extract(this, i0), extract(this, i1), GetLerpFactor(i0, i1, time));
    }
}
