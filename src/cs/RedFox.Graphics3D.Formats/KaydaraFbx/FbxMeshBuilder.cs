using System.Numerics;
using System.Runtime.InteropServices;
using RedFox.Graphics3D.Buffers;

namespace RedFox.Graphics3D.Formats.KaydaraFbx;

/// <summary>
/// Accumulates triangle corners into a welded vertex list, sharing a vertex between corners with the same control point and attributes.
/// </summary>
public sealed class FbxMeshBuilder
{
    private readonly int[]			_heads;
    private readonly List<int>		_next		= [];
    private readonly List<float>	_attributes	= [];
    private readonly List<int>		_indices	= [];
    private readonly int			_stride;

    /// <summary>
    /// Gets the control point of each emitted vertex.
    /// </summary>
    public List<int> ControlPoints { get; } = [];

    /// <summary>
    /// Initializes a new <see cref="FbxMeshBuilder"/>.
    /// </summary>
    /// <param name="controlPointCount">The number of control points in the source geometry.</param>
    /// <param name="stride">The number of floats in a corner's attribute block.</param>
    public FbxMeshBuilder(int controlPointCount, int stride)
    {
        _heads	= new int[controlPointCount];
        _stride	= stride;

        Array.Fill(_heads, -1);
    }

    /// <summary>
    /// Adds a triangle corner, reusing an existing vertex when one matches.
    /// </summary>
    /// <param name="controlPoint">The control point of the corner.</param>
    /// <param name="attributes">The corner's attribute block.</param>
    public void AddCorner(int controlPoint, ReadOnlySpan<float> attributes)
    {
        ReadOnlySpan<float> stored = CollectionsMarshal.AsSpan(_attributes);

        for (int vertex = _heads[controlPoint]; vertex >= 0; vertex = _next[vertex])
        {
            if (stored.Slice(vertex * _stride, _stride).SequenceEqual(attributes))
            {
                _indices.Add(vertex);
                return;
            }
        }

        int created = ControlPoints.Count;

        ControlPoints.Add(controlPoint);
        _next.Add(_heads[controlPoint]);
        _heads[controlPoint] = created;
        _attributes.AddRange(attributes);
        _indices.Add(created);
    }

    /// <summary>
    /// Writes the accumulated vertices and triangles into a mesh.
    /// </summary>
    /// <param name="mesh">The destination mesh.</param>
    /// <param name="positions">The control point positions.</param>
    /// <param name="layers">The layer layout used to build the attribute blocks.</param>
    public void Build(Mesh mesh, Vector3[] positions, FbxMeshLayers layers)
    {
        int					count		= ControlPoints.Count;
        float[]				vertices	= new float[count * 3];
        ReadOnlySpan<float>	attributes	= CollectionsMarshal.AsSpan(_attributes);

        for (int i = 0; i < count; i++)
        {
            Vector3 position = positions[ControlPoints[i]];

            vertices[i * 3]		= position.X;
            vertices[i * 3 + 1]	= position.Y;
            vertices[i * 3 + 2]	= position.Z;
        }

        mesh.Positions		= new DataBuffer<float>(vertices, 1, 3);
        mesh.FaceIndices	= new DataBuffer<int>(_indices.ToArray(), 1, 1);

        if (layers.NormalOffset >= 0)
            mesh.Normals = new DataBuffer<float>(Gather(attributes, count, layers.NormalOffset, 3), 1, 3);

        if (layers.TangentOffset >= 0)
            mesh.Tangents = new DataBuffer<float>(Gather(attributes, count, layers.TangentOffset, 4), 1, 4);

        if (layers.BinormalOffset >= 0)
            mesh.BiTangents = new DataBuffer<float>(Gather(attributes, count, layers.BinormalOffset, 3), 1, 3);

        if (layers.UVs.Count > 0)
            mesh.UVLayers = new DataBuffer<float>(Gather(attributes, count, layers.UVOffset, layers.UVs.Count * 2), layers.UVs.Count, 2);

        if (layers.Colors.Count > 0)
            mesh.ColorLayers = new DataBuffer<float>(Gather(attributes, count, layers.ColorOffset, layers.Colors.Count * 4), layers.Colors.Count, 4);
    }

    private float[] Gather(ReadOnlySpan<float> attributes, int count, int offset, int size)
    {
        float[] values = new float[count * size];

        for (int i = 0; i < count; i++)
            attributes.Slice(i * _stride + offset, size).CopyTo(values.AsSpan(i * size, size));

        return values;
    }
}
