using System.Globalization;
using System.Text;

namespace RedFox.Graphics3D.Formats.KaydaraFbx;

/// <summary>
/// Represents a single typed FBX property value without boxing scalar values.
/// </summary>
public readonly struct FbxProperty
{
    private readonly long		_bits;
    private readonly object?	_reference;

    /// <summary>
    /// Gets the encoded type of the property.
    /// </summary>
    public FbxPropertyType Type { get; }

    /// <summary>
    /// Gets a value indicating whether the property holds an array.
    /// </summary>
    public bool IsArray => Type is FbxPropertyType.SingleArray or FbxPropertyType.DoubleArray or FbxPropertyType.Int32Array or FbxPropertyType.Int64Array or FbxPropertyType.BooleanArray or FbxPropertyType.ByteArray;

    /// <summary>
    /// Gets a value indicating whether the property holds a scalar number or boolean.
    /// </summary>
    public bool IsNumber => Type is FbxPropertyType.Byte or FbxPropertyType.Int16 or FbxPropertyType.Boolean or FbxPropertyType.Int32 or FbxPropertyType.Int64 or FbxPropertyType.Single or FbxPropertyType.Double;

    /// <summary>
    /// Gets the number of elements when the property holds an array, otherwise zero.
    /// </summary>
    public int ArrayLength => IsArray ? ((Array)_reference!).Length : 0;

    /// <summary>
    /// Gets the property value as an object, boxing scalar values.
    /// </summary>
    public object Value => Type switch
    {
        FbxPropertyType.Byte	=> (byte)_bits,
        FbxPropertyType.Int16	=> (short)_bits,
        FbxPropertyType.Boolean	=> _bits != 0,
        FbxPropertyType.Int32	=> (int)_bits,
        FbxPropertyType.Int64	=> _bits,
        FbxPropertyType.Single	=> (float)BitConverter.Int64BitsToDouble(_bits),
        FbxPropertyType.Double	=> BitConverter.Int64BitsToDouble(_bits),
        _						=> _reference!,
    };

    /// <summary>
    /// Initializes a new <see cref="FbxProperty"/> holding an unsigned 8-bit integer.
    /// </summary>
    /// <param name="value">The value.</param>
    public FbxProperty(byte value) : this(FbxPropertyType.Byte, value, null)
    {
    }

    /// <summary>
    /// Initializes a new <see cref="FbxProperty"/> holding a 16-bit integer.
    /// </summary>
    /// <param name="value">The value.</param>
    public FbxProperty(short value) : this(FbxPropertyType.Int16, value, null)
    {
    }

    /// <summary>
    /// Initializes a new <see cref="FbxProperty"/> holding a boolean.
    /// </summary>
    /// <param name="value">The value.</param>
    public FbxProperty(bool value) : this(FbxPropertyType.Boolean, value ? 1 : 0, null)
    {
    }

    /// <summary>
    /// Initializes a new <see cref="FbxProperty"/> holding a 32-bit integer.
    /// </summary>
    /// <param name="value">The value.</param>
    public FbxProperty(int value) : this(FbxPropertyType.Int32, value, null)
    {
    }

    /// <summary>
    /// Initializes a new <see cref="FbxProperty"/> holding a 64-bit integer.
    /// </summary>
    /// <param name="value">The value.</param>
    public FbxProperty(long value) : this(FbxPropertyType.Int64, value, null)
    {
    }

    /// <summary>
    /// Initializes a new <see cref="FbxProperty"/> holding a 32-bit float.
    /// </summary>
    /// <param name="value">The value.</param>
    public FbxProperty(float value) : this(FbxPropertyType.Single, BitConverter.DoubleToInt64Bits(value), null)
    {
    }

    /// <summary>
    /// Initializes a new <see cref="FbxProperty"/> holding a 64-bit float.
    /// </summary>
    /// <param name="value">The value.</param>
    public FbxProperty(double value) : this(FbxPropertyType.Double, BitConverter.DoubleToInt64Bits(value), null)
    {
    }

    /// <summary>
    /// Initializes a new <see cref="FbxProperty"/> holding a string.
    /// </summary>
    /// <param name="value">The value.</param>
    public FbxProperty(string value) : this(FbxPropertyType.String, 0, value)
    {
    }

    /// <summary>
    /// Initializes a new <see cref="FbxProperty"/> holding raw binary data.
    /// </summary>
    /// <param name="value">The value.</param>
    public FbxProperty(byte[] value) : this(FbxPropertyType.Raw, 0, value)
    {
    }

    /// <summary>
    /// Initializes a new <see cref="FbxProperty"/> holding a 32-bit float array.
    /// </summary>
    /// <param name="value">The value.</param>
    public FbxProperty(float[] value) : this(FbxPropertyType.SingleArray, 0, value)
    {
    }

    /// <summary>
    /// Initializes a new <see cref="FbxProperty"/> holding a 64-bit float array.
    /// </summary>
    /// <param name="value">The value.</param>
    public FbxProperty(double[] value) : this(FbxPropertyType.DoubleArray, 0, value)
    {
    }

    /// <summary>
    /// Initializes a new <see cref="FbxProperty"/> holding a 32-bit integer array.
    /// </summary>
    /// <param name="value">The value.</param>
    public FbxProperty(int[] value) : this(FbxPropertyType.Int32Array, 0, value)
    {
    }

    /// <summary>
    /// Initializes a new <see cref="FbxProperty"/> holding a 64-bit integer array.
    /// </summary>
    /// <param name="value">The value.</param>
    public FbxProperty(long[] value) : this(FbxPropertyType.Int64Array, 0, value)
    {
    }

    /// <summary>
    /// Initializes a new <see cref="FbxProperty"/> holding a boolean array.
    /// </summary>
    /// <param name="value">The value.</param>
    public FbxProperty(bool[] value) : this(FbxPropertyType.BooleanArray, 0, value)
    {
    }

    private FbxProperty(FbxPropertyType type, long bits, object? reference)
    {
        Type		= type;
        _bits		= bits;
        _reference	= reference;
    }

    /// <summary>
    /// Creates a boolean property that keeps the raw byte, which some files use to store a character such as <c>T</c> or <c>Y</c>.
    /// </summary>
    /// <param name="value">The raw byte.</param>
    /// <returns>The created property.</returns>
    public static FbxProperty CreateBoolean(byte value)
    {
        return new FbxProperty(FbxPropertyType.Boolean, value, null);
    }

    /// <summary>
    /// Creates a property holding a byte array, as encoded by the <c>c</c> type code.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The created property.</returns>
    public static FbxProperty CreateByteArray(byte[] value)
    {
        return new FbxProperty(FbxPropertyType.ByteArray, 0, value);
    }

    /// <summary>
    /// Returns the property as a 64-bit integer.
    /// </summary>
    /// <returns>The converted value.</returns>
    public long AsInt64()
    {
        return Type switch
        {
            FbxPropertyType.Byte or FbxPropertyType.Int16 or FbxPropertyType.Boolean or FbxPropertyType.Int32 or FbxPropertyType.Int64 => _bits,
            FbxPropertyType.Single or FbxPropertyType.Double => (long)BitConverter.Int64BitsToDouble(_bits),
            FbxPropertyType.String when long.TryParse((string)_reference!, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed) => parsed,
            _ => throw new InvalidDataException($"FBX property of type '{(char)Type}' is not an integer."),
        };
    }

    /// <summary>
    /// Returns the property as a 32-bit integer.
    /// </summary>
    /// <returns>The converted value.</returns>
    public int AsInt32()
    {
        return (int)AsInt64();
    }

    /// <summary>
    /// Returns the property as a boolean.
    /// </summary>
    /// <returns>The converted value.</returns>
    public bool AsBoolean()
    {
        return Type is FbxPropertyType.Single or FbxPropertyType.Double ? AsDouble() != 0.0 : AsInt64() != 0;
    }

    /// <summary>
    /// Returns the property as a 64-bit float.
    /// </summary>
    /// <returns>The converted value.</returns>
    public double AsDouble()
    {
        return Type switch
        {
            FbxPropertyType.Byte or FbxPropertyType.Int16 or FbxPropertyType.Boolean or FbxPropertyType.Int32 or FbxPropertyType.Int64 => _bits,
            FbxPropertyType.Single or FbxPropertyType.Double => BitConverter.Int64BitsToDouble(_bits),
            FbxPropertyType.String when double.TryParse((string)_reference!, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) => parsed,
            _ => throw new InvalidDataException($"FBX property of type '{(char)Type}' is not a number."),
        };
    }

    /// <summary>
    /// Returns the property as a 32-bit float.
    /// </summary>
    /// <returns>The converted value.</returns>
    public float AsSingle()
    {
        return (float)AsDouble();
    }

    /// <summary>
    /// Returns the property as a string, decoding raw data as UTF-8 and formatting numbers invariantly.
    /// </summary>
    /// <returns>The converted value.</returns>
    public string AsString()
    {
        return Type switch
        {
            FbxPropertyType.String	=> (string)_reference!,
            FbxPropertyType.Raw		=> Encoding.UTF8.GetString((byte[])_reference!),
            FbxPropertyType.Single	or FbxPropertyType.Double => AsDouble().ToString("R", CultureInfo.InvariantCulture),
            _ when IsNumber			=> _bits.ToString(CultureInfo.InvariantCulture),
            _						=> throw new InvalidDataException($"FBX property of type '{(char)Type}' is not a string."),
        };
    }

    /// <summary>
    /// Returns the property as raw bytes, encoding strings as UTF-8.
    /// </summary>
    /// <returns>The converted value.</returns>
    public byte[] AsBytes()
    {
        return Type switch
        {
            FbxPropertyType.Raw or FbxPropertyType.ByteArray => (byte[])_reference!,
            FbxPropertyType.String => Encoding.UTF8.GetBytes((string)_reference!),
            _ => throw new InvalidDataException($"FBX property of type '{(char)Type}' is not binary data."),
        };
    }

    /// <summary>
    /// Returns the property as a 64-bit float array, converting other numeric arrays.
    /// </summary>
    /// <returns>The stored array when it already has the requested type, otherwise a converted copy.</returns>
    public double[] ToDoubleArray()
    {
        return _reference switch
        {
            double[] values	=> values,
            float[] values	=> Array.ConvertAll(values, static value => (double)value),
            int[] values	=> Array.ConvertAll(values, static value => (double)value),
            long[] values	=> Array.ConvertAll(values, static value => (double)value),
            bool[] values	=> Array.ConvertAll(values, static value => value ? 1.0 : 0.0),
            _ when IsNumber	=> [AsDouble()],
            _				=> throw new InvalidDataException($"FBX property of type '{(char)Type}' is not a numeric array."),
        };
    }

    /// <summary>
    /// Returns the property as a 32-bit float array, converting other numeric arrays.
    /// </summary>
    /// <returns>The stored array when it already has the requested type, otherwise a converted copy.</returns>
    public float[] ToSingleArray()
    {
        return _reference switch
        {
            float[] values	=> values,
            double[] values	=> Array.ConvertAll(values, static value => (float)value),
            int[] values	=> Array.ConvertAll(values, static value => (float)value),
            long[] values	=> Array.ConvertAll(values, static value => (float)value),
            bool[] values	=> Array.ConvertAll(values, static value => value ? 1.0f : 0.0f),
            _ when IsNumber	=> [AsSingle()],
            _				=> throw new InvalidDataException($"FBX property of type '{(char)Type}' is not a numeric array."),
        };
    }

    /// <summary>
    /// Returns the property as a 32-bit integer array, converting other numeric arrays.
    /// </summary>
    /// <returns>The stored array when it already has the requested type, otherwise a converted copy.</returns>
    public int[] ToInt32Array()
    {
        return _reference switch
        {
            int[] values	=> values,
            long[] values	=> Array.ConvertAll(values, static value => (int)value),
            double[] values	=> Array.ConvertAll(values, static value => (int)value),
            float[] values	=> Array.ConvertAll(values, static value => (int)value),
            bool[] values	=> Array.ConvertAll(values, static value => value ? 1 : 0),
            byte[] values	when Type == FbxPropertyType.ByteArray => Array.ConvertAll(values, static value => (int)value),
            _ when IsNumber	=> [AsInt32()],
            _				=> throw new InvalidDataException($"FBX property of type '{(char)Type}' is not a numeric array."),
        };
    }

    /// <summary>
    /// Returns the property as a 64-bit integer array, converting other numeric arrays.
    /// </summary>
    /// <returns>The stored array when it already has the requested type, otherwise a converted copy.</returns>
    public long[] ToInt64Array()
    {
        return _reference switch
        {
            long[] values	=> values,
            int[] values	=> Array.ConvertAll(values, static value => (long)value),
            double[] values	=> Array.ConvertAll(values, static value => (long)value),
            float[] values	=> Array.ConvertAll(values, static value => (long)value),
            bool[] values	=> Array.ConvertAll(values, static value => value ? 1L : 0L),
            byte[] values when Type == FbxPropertyType.ByteArray => Array.ConvertAll(values, static value => (long)value),
            _ when IsNumber	=> [AsInt64()],
            _				=> throw new InvalidDataException($"FBX property of type '{(char)Type}' is not a numeric array."),
        };
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return IsArray ? $"{(char)Type}[{ArrayLength}]" : $"{(char)Type}: {Value}";
    }
}
