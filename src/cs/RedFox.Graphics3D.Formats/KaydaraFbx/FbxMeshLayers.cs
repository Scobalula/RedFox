using System.Numerics;

namespace RedFox.Graphics3D.Formats.KaydaraFbx;

/// <summary>
/// Collects the layer elements of an FBX geometry and writes the per-corner attribute block used for vertex welding.
/// </summary>
public sealed class FbxMeshLayers
{
    private const int MaxLayers = 16;

    private readonly Matrix4x4 _geometric;
    private readonly Matrix4x4 _normalMatrix;

    /// <summary>
    /// Gets the normal element, if any.
    /// </summary>
    public FbxLayerElement? Normals { get; }

    /// <summary>
    /// Gets the tangent element, if any.
    /// </summary>
    public FbxLayerElement? Tangents { get; }

    /// <summary>
    /// Gets the binormal element, if any.
    /// </summary>
    public FbxLayerElement? Binormals { get; }

    /// <summary>
    /// Gets the UV set elements.
    /// </summary>
    public List<FbxLayerElement> UVs { get; } = [];

    /// <summary>
    /// Gets the vertex color elements.
    /// </summary>
    public List<FbxLayerElement> Colors { get; } = [];

    /// <summary>
    /// Gets the per-polygon material element, if any.
    /// </summary>
    public FbxLayerElement? Materials { get; }

    /// <summary>
    /// Gets the offset of the normal in the attribute block, or -1.
    /// </summary>
    public int NormalOffset { get; }

    /// <summary>
    /// Gets the offset of the four-component tangent in the attribute block, or -1.
    /// </summary>
    public int TangentOffset { get; }

    /// <summary>
    /// Gets the offset of the binormal in the attribute block, or -1.
    /// </summary>
    public int BinormalOffset { get; }

    /// <summary>
    /// Gets the offset of the first UV set in the attribute block.
    /// </summary>
    public int UVOffset { get; }

    /// <summary>
    /// Gets the offset of the first color set in the attribute block.
    /// </summary>
    public int ColorOffset { get; }

    /// <summary>
    /// Gets the number of floats in a corner's attribute block.
    /// </summary>
    public int Stride { get; }

    /// <summary>
    /// Initializes a new <see cref="FbxMeshLayers"/> from a geometry node.
    /// </summary>
    /// <param name="geometry">The geometry node holding the layer elements.</param>
    /// <param name="geometric">The geometric transform baked into the geometry.</param>
    public FbxMeshLayers(FbxNode geometry, Matrix4x4 geometric)
    {
        _geometric		= geometric;
        _normalMatrix	= Matrix4x4.Invert(geometric, out Matrix4x4 inverse) ? Matrix4x4.Transpose(inverse) : geometric;

        foreach (FbxNode element in geometry.Children)
        {
            switch (element.Name)
            {
                case "LayerElementNormal" when Normals is null:
                    Normals = FbxLayerElement.Read(element, "Normals", "NormalsIndex", 3);
                    break;
                case "LayerElementTangent" when Tangents is null:
                    Tangents = FbxLayerElement.Read(element, "Tangents", "TangentsIndex", 3);
                    break;
                case "LayerElementBinormal" when Binormals is null:
                    Binormals = FbxLayerElement.Read(element, "Binormals", "BinormalsIndex", 3);
                    break;
                case "LayerElementMaterial" when Materials is null:
                    Materials = FbxLayerElement.Read(element, "Materials", "Materials", 1);
                    break;
                case "LayerElementUV" when UVs.Count < MaxLayers && FbxLayerElement.Read(element, "UV", "UVIndex", 2) is FbxLayerElement uv:
                    UVs.Add(uv);
                    break;
                case "LayerElementColor" when Colors.Count < MaxLayers && FbxLayerElement.Read(element, "Colors", "ColorIndex", 4) is FbxLayerElement color:
                    Colors.Add(color);
                    break;
            }
        }

        int stride = 0;

        NormalOffset	= Normals is null ? -1 : Advance(ref stride, 3);
        TangentOffset	= Tangents is null ? -1 : Advance(ref stride, 4);
        BinormalOffset	= Binormals is null ? -1 : Advance(ref stride, 3);
        UVOffset		= Advance(ref stride, UVs.Count * 2);
        ColorOffset		= Advance(ref stride, Colors.Count * 4);
        Stride			= stride;
    }

    /// <summary>
    /// Returns the material index of a polygon.
    /// </summary>
    /// <param name="polygon">The polygon index.</param>
    /// <returns>The material index, or zero when the geometry has no material element.</returns>
    public int GetMaterial(int polygon)
    {
        if (Materials is null)
            return 0;

        int index = Materials.Mapping == FbxMappingMode.AllSame ? 0 : polygon;
        return (uint)index < (uint)Materials.Values.Length ? (int)Materials.Values[index] : 0;
    }

    /// <summary>
    /// Writes the attribute block of a polygon corner.
    /// </summary>
    /// <param name="corner">The polygon-vertex index of the corner.</param>
    /// <param name="controlPoint">The control point used by the corner.</param>
    /// <param name="polygon">The polygon containing the corner.</param>
    /// <param name="attributes">The destination block of <see cref="Stride"/> floats.</param>
    public void GetAttributes(int corner, int controlPoint, int polygon, Span<float> attributes)
    {
        attributes.Clear();

        Vector3 normal		= ReadDirection(Normals, corner, controlPoint, polygon, _normalMatrix);
        Vector3 tangent		= ReadDirection(Tangents, corner, controlPoint, polygon, _geometric);
        Vector3 binormal	= ReadDirection(Binormals, corner, controlPoint, polygon, _geometric);

        if (NormalOffset >= 0)
            Write(attributes[NormalOffset..], normal);

        if (TangentOffset >= 0)
        {
            Write(attributes[TangentOffset..], tangent);
            attributes[TangentOffset + 3] = Binormals is null || Vector3.Dot(Vector3.Cross(normal, tangent), binormal) >= 0.0f ? 1.0f : -1.0f;
        }

        if (BinormalOffset >= 0)
            Write(attributes[BinormalOffset..], binormal);

        for (int i = 0; i < UVs.Count; i++)
        {
            Span<float> uv = attributes.Slice(UVOffset + i * 2, 2);

            if (ReadValues(UVs[i], corner, controlPoint, polygon, uv))
                uv[1] = 1.0f - uv[1];
        }

        for (int i = 0; i < Colors.Count; i++)
            ReadValues(Colors[i], corner, controlPoint, polygon, attributes.Slice(ColorOffset + i * 4, 4));
    }

    private static int Advance(ref int stride, int size)
    {
        int offset = stride;
        stride += size;
        return offset;
    }

    private static Vector3 ReadDirection(FbxLayerElement? element, int corner, int controlPoint, int polygon, Matrix4x4 transform)
    {
        if (element is null || element.ResolveIndex(corner, controlPoint, polygon, -1) is not (>= 0 and var index))
            return Vector3.Zero;

        Vector3	value		= new((float)element.Values[index * 3], (float)element.Values[index * 3 + 1], (float)element.Values[index * 3 + 2]);
        Vector3	transformed	= Vector3.TransformNormal(value, transform);
        float	length		= transformed.Length();

        return length > 0.0f ? transformed / length : transformed;
    }

    private static bool ReadValues(FbxLayerElement element, int corner, int controlPoint, int polygon, Span<float> destination)
    {
        int index = element.ResolveIndex(corner, controlPoint, polygon, -1);

        if (index < 0)
            return false;

        for (int i = 0; i < destination.Length; i++)
            destination[i] = (float)element.Values[index * element.ComponentCount + i];

        return true;
    }

    private static void Write(Span<float> destination, Vector3 value)
    {
        destination[0] = value.X;
        destination[1] = value.Y;
        destination[2] = value.Z;
    }
}
