namespace RedFox.Graphics3D.Formats.KaydaraFbx;

/// <summary>
/// Represents a single FBX animation curve and evaluates it with constant, linear or weighted cubic interpolation.
/// </summary>
public sealed class FbxAnimationCurve
{
    private const int	InterpolationConstant	= 0x00000002;
    private const int	InterpolationCubic		= 0x00000008;
    private const int	ConstantNext			= 0x00000100;
    private const int	WeightedRight			= 0x01000000;
    private const int	WeightedNextLeft		= 0x02000000;
    private const float	DefaultWeight			= 1.0f / 3.0f;

    private readonly long[]		_times;
    private readonly float[]	_values;
    private readonly int[]		_flags;
    private readonly float[]	_rightSlopes;
    private readonly float[]	_leftSlopes;
    private readonly float[]	_rightWeights;
    private readonly float[]	_leftWeights;

    private FbxCurveExtrapolation _preExtrapolation		= FbxCurveExtrapolation.Constant;
    private FbxCurveExtrapolation _postExtrapolation	= FbxCurveExtrapolation.Constant;

    /// <summary>
    /// Gets the number of FBX time ticks per second.
    /// </summary>
    public const long TicksPerSecond = 46186158000;

    /// <summary>
    /// Gets the number of keys.
    /// </summary>
    public int Count => _times.Length;

    /// <summary>
    /// Gets the time of the first key in ticks.
    /// </summary>
    public long FirstTime => _times.Length > 0 ? _times[0] : 0;

    /// <summary>
    /// Gets the time of the last key in ticks.
    /// </summary>
    public long LastTime => _times.Length > 0 ? _times[^1] : 0;

    private FbxAnimationCurve(long[] times, float[] values)
    {
        _times			= times;
        _values			= values;
        _flags			= new int[times.Length];
        _rightSlopes	= new float[times.Length];
        _leftSlopes		= new float[times.Length];
        _rightWeights	= new float[times.Length];
        _leftWeights	= new float[times.Length];

        Array.Fill(_rightWeights, DefaultWeight);
        Array.Fill(_leftWeights, DefaultWeight);
    }

    /// <summary>
    /// Reads an FBX 7 <c>AnimationCurve</c> object.
    /// </summary>
    /// <param name="curve">The curve object.</param>
    /// <returns>The curve, or <see langword="null"/> when it has no keys.</returns>
    public static FbxAnimationCurve? Read(FbxObject curve)
    {
        long[]	times	= curve.Node.FirstChild("KeyTime")?.GetInt64s() ?? [];
        float[]	values	= curve.Node.FirstChild("KeyValueFloat")?.GetSingles() ?? [];
        int		count	= Math.Min(times.Length, values.Length);

        if (count == 0)
            return null;

        FbxAnimationCurve result = new(times[..count], values[..count]);

        int[]		flags		= curve.GetChildInt32s("KeyAttrFlags");
        float[]		data		= ReadAttributeData(curve.Node.FirstChild("KeyAttrDataFloat"));
        int[]		references	= curve.GetChildInt32s("KeyAttrRefCount");
        int			key			= 0;

        result._preExtrapolation	= FbxCurveExtrapolation.Read(curve.Node.FirstChild("Pre-Extrapolation"));
        result._postExtrapolation	= FbxCurveExtrapolation.Read(curve.Node.FirstChild("Post-Extrapolation"));

        for (int attribute = 0; attribute < flags.Length && key < count; attribute++)
        {
            int repeat = attribute < references.Length ? Math.Max(references[attribute], 1) : count - key;

            for (int i = 0; i < repeat && key < count; i++, key++)
            {
                result._flags[key] = flags[attribute];

                if (attribute * 4 + 3 >= data.Length)
                    continue;

                int packedWeights = BitConverter.SingleToInt32Bits(data[attribute * 4 + 2]);

                result._rightSlopes[key]	= data[attribute * 4];
                result._leftSlopes[key]		= data[attribute * 4 + 1];

                if ((flags[attribute] & WeightedRight) != 0)
                    result._rightWeights[key] = (packedWeights & 0xFFFF) / 9999.0f;

                if ((flags[attribute] & WeightedNextLeft) != 0)
                    result._leftWeights[key] = ((packedWeights >> 16) & 0xFFFF) / 9999.0f;
            }
        }

        return result;
    }

    /// <summary>
    /// Reads the <c>Key</c> list of an FBX 6 take channel.
    /// </summary>
    /// <param name="keys">The <c>Key</c> node.</param>
    /// <returns>The curve, or <see langword="null"/> when it has no keys.</returns>
    public static FbxAnimationCurve? ReadLegacy(FbxNode keys)
    {
        List<FbxProperty>	properties	= keys.Properties;
        List<long>			times		= [];
        List<float>			values		= [];
        List<int>			flags		= [];
        List<float>			slopes		= [];
        List<float>			weights		= [];

        for (int i = 0; i + 1 < properties.Count;)
        {
            if (!IsValue(properties[i]) || !IsValue(properties[i + 1]))
            {
                i++;
                continue;
            }

            times.Add(properties[i].AsInt64());
            values.Add(properties[i + 1].AsSingle());
            i += 2;

            char	mode		= TryGetMode(properties, ref i, out char interpolation) ? interpolation : 'L';
            float	rightSlope	= 0.0f;
            float	leftSlope	= 0.0f;
            float	rightWeight	= DefaultWeight;
            float	leftWeight	= DefaultWeight;

            if (mode == 'C')
                TryGetMode(properties, ref i, out _);

            if (mode == 'U' && TryGetMode(properties, ref i, out char tangent))
            {
                int slopeCount = tangent is 's' or 'b' ? 2 : tangent == 'p' ? 3 : 0;

                rightSlope	= ReadValue(properties, ref i, slopeCount > 0, 0.0f);
                leftSlope	= ReadValue(properties, ref i, slopeCount > 1, 0.0f);
                ReadValue(properties, ref i, slopeCount > 2, 0.0f);

                if (TryGetMode(properties, ref i, out char weight))
                {
                    rightWeight	= ReadValue(properties, ref i, weight is 'a' or 'r', DefaultWeight);
                    leftWeight	= ReadValue(properties, ref i, weight is 'a' or 'l', DefaultWeight);
                }
            }

            flags.Add(mode == 'C' ? InterpolationConstant : mode == 'U' ? InterpolationCubic : 0);
            slopes.Add(rightSlope);
            slopes.Add(leftSlope);
            weights.Add(rightWeight);
            weights.Add(leftWeight);
        }

        if (times.Count == 0)
            return null;

        FbxAnimationCurve result = new([.. times], [.. values]);

        for (int i = 0; i < times.Count; i++)
        {
            result._flags[i]		= flags[i];
            result._rightSlopes[i]	= slopes[i * 2];
            result._leftSlopes[i]	= slopes[i * 2 + 1];
            result._rightWeights[i]	= weights[i * 2];
            result._leftWeights[i]	= weights[i * 2 + 1];
        }

        return result;
    }

    /// <summary>
    /// Evaluates the curve at a time.
    /// </summary>
    /// <param name="time">The time in FBX ticks.</param>
    /// <returns>The curve value.</returns>
    public float Evaluate(long time)
    {
        long first		= _times[0];
        long last		= _times[^1];
        long duration	= last - first;

        if (time < first && duration > 0 && _preExtrapolation.Type != 'C')
            return Extrapolate(first - time, _preExtrapolation, false);

        if (time > last && duration > 0 && _postExtrapolation.Type != 'C')
            return Extrapolate(time - last, _postExtrapolation, true);

        return EvaluateKeys(time);
    }

    private static bool IsValue(FbxProperty property)
    {
        return property.IsNumber && property.Type != FbxPropertyType.Boolean;
    }

    private static bool TryGetMode(List<FbxProperty> properties, ref int index, out char mode)
    {
        mode = '\0';

        if (index >= properties.Count)
            return false;

        if (properties[index].Type == FbxPropertyType.String && properties[index].AsString().Length > 0)
            mode = properties[index].AsString()[0];
        else if (properties[index].Type == FbxPropertyType.Boolean)
            mode = (char)properties[index].AsInt64();
        else
            return false;

        index++;
        return true;
    }

    private static float ReadValue(List<FbxProperty> properties, ref int index, bool isPresent, float fallback)
    {
        if (!isPresent || index >= properties.Count || !IsValue(properties[index]))
            return fallback;

        return properties[index++].AsSingle();
    }

    private static float[] ReadAttributeData(FbxNode? node)
    {
        if (node is null)
            return [];

        if (node.Properties.Count == 1 && node.Properties[0].Type is FbxPropertyType.Int32Array or FbxPropertyType.Int64Array)
            return Array.ConvertAll(node.GetInt32s(), BitConverter.Int32BitsToSingle);

        return node.GetSingles();
    }

    private float Extrapolate(long offset, FbxCurveExtrapolation extrapolation, bool isPost)
    {
        long first		= _times[0];
        long last		= _times[^1];
        long duration	= last - first;

        if (extrapolation.Repetition == 0)
            return isPost ? _values[^1] : _values[0];

        if (extrapolation.Type == 'K')
        {
            int		end		= isPost ? _times.Length - 1 : 0;
            int		other	= isPost ? end - 1 : 1;
            double	slope	= (_values[end] - _values[other]) / (double)(_times[end] - _times[other]);

            return (float)(_values[end] + slope * (isPost ? offset : -offset));
        }

        long cycle = (offset - 1) / duration;

        if (extrapolation.Repetition > 0 && cycle >= extrapolation.Repetition)
        {
            cycle	= extrapolation.Repetition - 1;
            offset	= duration * extrapolation.Repetition;
        }

        long	local		= offset - cycle * duration;
        bool	mirrored	= extrapolation.Type == 'M' && cycle % 2 == 0;
        long	time		= isPost != mirrored ? first + local : last - local;
        float	value		= EvaluateKeys(time);

        if (extrapolation.Type != 'A')
            return value;

        float range = _values[^1] - _values[0];
        return value + (cycle + 1) * (isPost ? range : -range);
    }

    private float EvaluateKeys(long time)
    {
        if (time <= _times[0])
            return _values[0];

        if (time >= _times[^1])
            return _values[^1];

        int index = Array.BinarySearch(_times, time);

        if (index >= 0)
            return _values[index];

        int		next		= ~index;
        int		previous	= next - 1;
        int		flags		= _flags[previous];
        long	duration	= _times[next] - _times[previous];
        float	fraction	= (float)((double)(time - _times[previous]) / duration);

        if ((flags & InterpolationConstant) != 0)
            return (flags & ConstantNext) != 0 ? _values[next] : _values[previous];

        if ((flags & InterpolationCubic) == 0)
            return _values[previous] + (_values[next] - _values[previous]) * fraction;

        float seconds	= (float)((double)duration / TicksPerSecond);
        float startTime	= _rightWeights[previous];
        float endTime	= 1.0f - _leftWeights[previous];
        float start		= _values[previous] + _rightSlopes[previous] * seconds * _rightWeights[previous];
        float end		= _values[next] - _leftSlopes[previous] * seconds * _leftWeights[previous];
        float t			= SolveBezier(startTime, endTime, fraction);
        float inverse	= 1.0f - t;

        return inverse * inverse * inverse * _values[previous] + 3.0f * inverse * inverse * t * start + 3.0f * inverse * t * t * end + t * t * t * _values[next];
    }

    private static float SolveBezier(float startTime, float endTime, float fraction)
    {
        float low	= 0.0f;
        float high	= 1.0f;
        float t		= fraction;

        for (int i = 0; i < 24; i++)
        {
            float inverse	= 1.0f - t;
            float x			= 3.0f * inverse * inverse * t * startTime + 3.0f * inverse * t * t * endTime + t * t * t;

            if (MathF.Abs(x - fraction) < 1e-6f)
                break;

            if (x < fraction)
                low = t;
            else
                high = t;

            t = (low + high) * 0.5f;
        }

        return t;
    }
}
