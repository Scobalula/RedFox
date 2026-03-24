using System.Globalization;

namespace RedFox.Graphics3D.KaydaraFbx;

/// <summary>
/// Represents a single typed FBX property value.
/// </summary>
/// <param name="TypeCode">The FBX property type code (for example <c>I</c>, <c>D</c>, <c>S</c>).</param>
/// <param name="Value">The associated property value object.</param>
public sealed record FbxProperty(char TypeCode, object Value)
{
    /// <summary>
    /// Returns this property as a <see cref="long"/> when conversion is possible.
    /// </summary>
    public long AsInt64()
    {
        return Value switch
        {
            long l => l,
            int i => i,
            short s => s,
            byte b => b,
            bool flag => flag ? 1 : 0,
            _ => Convert.ToInt64(Value, CultureInfo.InvariantCulture),
        };
    }

    /// <summary>
    /// Returns this property as a <see cref="double"/> when conversion is possible.
    /// </summary>
    public double AsDouble()
    {
        return Value switch
        {
            double d => d,
            float f => f,
            long l => l,
            int i => i,
            _ => Convert.ToDouble(Value, CultureInfo.InvariantCulture),
        };
    }

    /// <summary>
    /// Returns this property as a UTF-8 string.
    /// </summary>
    public string AsString()
    {
        return Value switch
        {
            string s => s,
            byte[] bytes => System.Text.Encoding.UTF8.GetString(bytes),
            _ => Convert.ToString(Value, CultureInfo.InvariantCulture) ?? string.Empty,
        };
    }
}
