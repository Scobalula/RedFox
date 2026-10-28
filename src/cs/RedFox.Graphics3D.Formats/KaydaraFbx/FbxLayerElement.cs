namespace RedFox.Graphics3D.Formats.KaydaraFbx;

/// <summary>
/// Represents a decoded FBX geometry layer element such as normals, UVs, colors or material indices.
/// </summary>
public sealed class FbxLayerElement
{
    /// <summary>
    /// Gets the element name, such as a UV set name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets which mesh component each value belongs to.
    /// </summary>
    public FbxMappingMode Mapping { get; }

    /// <summary>
    /// Gets the flattened element values.
    /// </summary>
    public double[] Values { get; }

    /// <summary>
    /// Gets the indices into <see cref="Values"/>, or <see langword="null"/> when values are referenced directly.
    /// </summary>
    public int[]? Indices { get; }

    /// <summary>
    /// Gets the number of components per value.
    /// </summary>
    public int ComponentCount { get; }

    /// <summary>
    /// Initializes a new <see cref="FbxLayerElement"/>.
    /// </summary>
    /// <param name="name">The element name.</param>
    /// <param name="mapping">The mapping mode.</param>
    /// <param name="values">The flattened element values.</param>
    /// <param name="indices">The value indices, or <see langword="null"/>.</param>
    /// <param name="componentCount">The number of components per value.</param>
    public FbxLayerElement(string name, FbxMappingMode mapping, double[] values, int[]? indices, int componentCount)
    {
        Name			= name;
        Mapping			= mapping;
        Values			= values;
        Indices			= indices;
        ComponentCount	= componentCount;
    }

    /// <summary>
    /// Reads a layer element node.
    /// </summary>
    /// <param name="element">The <c>LayerElement*</c> node.</param>
    /// <param name="valuesName">The name of the child holding the values.</param>
    /// <param name="indicesName">The name of the child holding the indices, used for indexed reference modes.</param>
    /// <param name="componentCount">The number of components per value.</param>
    /// <returns>The decoded element, or <see langword="null"/> when it has no usable mapping or values.</returns>
    public static FbxLayerElement? Read(FbxNode element, string valuesName, string indicesName, int componentCount)
    {
        FbxMappingMode?	mapping	= element.TryGetChildProperty("MappingInformationType", out FbxProperty mappingName) && mappingName.Type == FbxPropertyType.String ? ParseMapping(mappingName.AsString()) : null;
        double[]		values	= element.FirstChild(valuesName)?.GetDoubles() ?? [];

        if (mapping is null || values.Length < componentCount)
            return null;

        bool	indexed	= element.TryGetChildProperty("ReferenceInformationType", out FbxProperty reference) && reference.Type == FbxPropertyType.String && reference.AsString() != "Direct";
        int[]?	indices	= indexed ? element.FirstChild(indicesName)?.GetInt32s() : null;
        string	name	= element.TryGetChildProperty("Name", out FbxProperty nameProperty) && nameProperty.Type == FbxPropertyType.String ? nameProperty.AsString() : string.Empty;

        return new FbxLayerElement(name, mapping.Value, values, indices, componentCount);
    }

    /// <summary>
    /// Resolves the value index used by a polygon corner.
    /// </summary>
    /// <param name="corner">The polygon-vertex index of the corner.</param>
    /// <param name="controlPoint">The control point used by the corner.</param>
    /// <param name="polygon">The polygon containing the corner.</param>
    /// <param name="edge">The edge starting at the corner, or -1 when unknown.</param>
    /// <returns>The value index, or -1 when the corner has no valid value.</returns>
    public int ResolveIndex(int corner, int controlPoint, int polygon, int edge)
    {
        int index = Mapping switch
        {
            FbxMappingMode.ByControlPoint	=> controlPoint,
            FbxMappingMode.ByPolygon		=> polygon,
            FbxMappingMode.ByEdge			=> edge,
            FbxMappingMode.AllSame			=> 0,
            _								=> corner,
        };

        if (Indices is not null)
            index = (uint)index < (uint)Indices.Length ? Indices[index] : -1;

        return (uint)index < (uint)(Values.Length / ComponentCount) ? index : -1;
    }

    private static FbxMappingMode? ParseMapping(string mapping)
    {
        return mapping switch
        {
            "ByPolygonVertex"								=> FbxMappingMode.ByPolygonVertex,
            "ByVertice" or "ByVertex" or "ByControlPoint"	=> FbxMappingMode.ByControlPoint,
            "ByPolygon"										=> FbxMappingMode.ByPolygon,
            "ByEdge"										=> FbxMappingMode.ByEdge,
            "AllSame"										=> FbxMappingMode.AllSame,
            _												=> null,
        };
    }
}
