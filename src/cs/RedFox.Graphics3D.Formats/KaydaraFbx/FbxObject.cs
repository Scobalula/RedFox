using System.Numerics;

namespace RedFox.Graphics3D.Formats.KaydaraFbx;

/// <summary>
/// Represents an FBX object from the <c>Objects</c> section with indexed access to its properties and template defaults.
/// </summary>
public sealed class FbxObject
{
    private readonly Dictionary<string, FbxNode> _properties;

    /// <summary>
    /// Gets the object identifier used by connections.
    /// </summary>
    public long Id { get; }

    /// <summary>
    /// Gets the object name without its class prefix or suffix.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the FBX node name of the object, such as <c>Model</c>, <c>Geometry</c> or <c>Material</c>.
    /// </summary>
    public string ClassName { get; }

    /// <summary>
    /// Gets the object sub-type, such as <c>Mesh</c>, <c>LimbNode</c> or <c>Cluster</c>.
    /// </summary>
    public string SubType { get; }

    /// <summary>
    /// Gets the FBX node that defines the object.
    /// </summary>
    public FbxNode Node { get; }

    /// <summary>
    /// Initializes a new <see cref="FbxObject"/>.
    /// </summary>
    /// <param name="id">The object identifier.</param>
    /// <param name="name">The object name.</param>
    /// <param name="subType">The object sub-type.</param>
    /// <param name="node">The FBX node that defines the object.</param>
    /// <param name="template">The template property values for the object class, or <see langword="null"/>.</param>
    public FbxObject(long id, string name, string subType, FbxNode node, IReadOnlyDictionary<string, FbxNode>? template)
    {
        Id			= id;
        Name		= name;
        ClassName	= node.Name;
        SubType		= subType;
        Node		= node;

        _properties = template is null ? new Dictionary<string, FbxNode>(StringComparer.Ordinal) : new Dictionary<string, FbxNode>(template, StringComparer.Ordinal);

        if ((node.FirstChild("Properties70") ?? node.FirstChild("Properties60")) is not FbxNode properties)
            return;

        foreach (FbxNode property in properties.Children)
        {
            if (property.Properties.Count > 0 && property.Properties[0].Type == FbxPropertyType.String)
                _properties[property.Properties[0].AsString()] = property;
        }
    }

    /// <summary>
    /// Splits a raw FBX object name into its name and class parts, handling both <c>Name\0\x01Class</c> and <c>Class::Name</c>.
    /// </summary>
    /// <param name="raw">The raw name stored in the file.</param>
    /// <returns>The object name without its class.</returns>
    public static string ParseName(string raw)
    {
        int separator = raw.IndexOf("\0\u0001", StringComparison.Ordinal);

        if (separator >= 0)
            return raw[..separator];

        separator = raw.IndexOf("::", StringComparison.Ordinal);
        return separator >= 0 ? raw[(separator + 2)..] : raw;
    }

    /// <summary>
    /// Reads the property definitions of a <c>Properties70</c> or <c>Properties60</c> node into a name-keyed dictionary.
    /// </summary>
    /// <param name="properties">The properties container node.</param>
    /// <returns>The property nodes keyed by property name.</returns>
    public static Dictionary<string, FbxNode> ReadPropertyTable(FbxNode properties)
    {
        Dictionary<string, FbxNode> table = new(StringComparer.Ordinal);

        foreach (FbxNode property in properties.Children)
        {
            if (property.Properties.Count > 0 && property.Properties[0].Type == FbxPropertyType.String)
                table.TryAdd(property.Properties[0].AsString(), property);
        }

        return table;
    }

    /// <summary>
    /// Determines whether the object or its template defines the named property.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <returns><see langword="true"/> when the property exists.</returns>
    public bool HasProperty(string name)
    {
        return _properties.ContainsKey(name);
    }

    /// <summary>
    /// Returns the value at the given index of a named property.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="index">The zero-based value index after the property header fields.</param>
    /// <param name="value">The property value.</param>
    /// <returns><see langword="true"/> when the value exists.</returns>
    public bool TryGetValue(string name, int index, out FbxProperty value)
    {
        if (_properties.TryGetValue(name, out FbxNode? property))
        {
            int offset = property.Name == "Property" ? 3 : 4;

            if (property.Properties.Count > offset + index)
            {
                value = property.Properties[offset + index];
                return true;
            }
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Returns a named numeric property as a double, or the fallback when it is missing.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="fallback">The value returned when the property is missing.</param>
    /// <returns>The property value.</returns>
    public double GetDouble(string name, double fallback)
    {
        return TryGetValue(name, 0, out FbxProperty value) && value.IsNumber ? value.AsDouble() : fallback;
    }

    /// <summary>
    /// Returns a named numeric property as a float, or the fallback when it is missing.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="fallback">The value returned when the property is missing.</param>
    /// <returns>The property value.</returns>
    public float GetSingle(string name, float fallback)
    {
        return (float)GetDouble(name, fallback);
    }

    /// <summary>
    /// Returns a named numeric property as an integer, or the fallback when it is missing.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="fallback">The value returned when the property is missing.</param>
    /// <returns>The property value.</returns>
    public int GetInt32(string name, int fallback)
    {
        return TryGetValue(name, 0, out FbxProperty value) && value.IsNumber ? value.AsInt32() : fallback;
    }

    /// <summary>
    /// Returns a named numeric property as a boolean, or the fallback when it is missing.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="fallback">The value returned when the property is missing.</param>
    /// <returns>The property value.</returns>
    public bool GetBoolean(string name, bool fallback)
    {
        return TryGetValue(name, 0, out FbxProperty value) && value.IsNumber ? value.AsBoolean() : fallback;
    }

    /// <summary>
    /// Returns a named string property, or the fallback when it is missing.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="fallback">The value returned when the property is missing.</param>
    /// <returns>The property value.</returns>
    public string GetString(string name, string fallback)
    {
        return TryGetValue(name, 0, out FbxProperty value) && value.Type == FbxPropertyType.String ? value.AsString() : fallback;
    }

    /// <summary>
    /// Returns a named three-component property, or the fallback when it is missing.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="fallback">The value returned when the property is missing.</param>
    /// <returns>The property value.</returns>
    public Vector3 GetVector3(string name, Vector3 fallback)
    {
        if (!TryGetValue(name, 0, out FbxProperty x) || !TryGetValue(name, 1, out FbxProperty y) || !TryGetValue(name, 2, out FbxProperty z) || !x.IsNumber || !y.IsNumber || !z.IsNumber)
            return fallback;

        return new Vector3(x.AsSingle(), y.AsSingle(), z.AsSingle());
    }

    /// <summary>
    /// Returns the numeric content of a named child node, or an empty array when the child is missing.
    /// </summary>
    /// <param name="name">The child node name.</param>
    /// <returns>The values as doubles.</returns>
    public double[] GetChildDoubles(string name)
    {
        return Node.FirstChild(name)?.GetDoubles() ?? [];
    }

    /// <summary>
    /// Returns the numeric content of a named child node, or an empty array when the child is missing.
    /// </summary>
    /// <param name="name">The child node name.</param>
    /// <returns>The values as integers.</returns>
    public int[] GetChildInt32s(string name)
    {
        return Node.FirstChild(name)?.GetInt32s() ?? [];
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"{ClassName}::{Name} ({SubType}, {Id})";
    }
}
