using System.Numerics;

namespace RedFox.Graphics3D;

/// <summary>
/// Provides helper methods for converting polygon face lists into triangle lists.
/// </summary>
public static class MeshTriangulator
{
    /// <summary>
    /// Triangulates FBX polygon-vertex indices where each face ends when a negative index appears.
    /// </summary>
    /// <param name="polygonVertexIndices">The FBX polygon-vertex index stream.</param>
    /// <returns>The triangle list and per-face triangle expansion counts.</returns>
    public static MeshTriangulationResult TriangulateFbxPolygonVertexIndices(ReadOnlySpan<int> polygonVertexIndices)
    {
        if (polygonVertexIndices.Length == 0)
        {
            return new MeshTriangulationResult([], []);
        }

        int faceCount = 0;
        int currentFaceVertexCount = 0;
        bool allTriangles = true;

        for (int i = 0; i < polygonVertexIndices.Length; i++)
        {
            currentFaceVertexCount++;
            if (polygonVertexIndices[i] >= 0)
            {
                continue;
            }

            faceCount++;
            if (currentFaceVertexCount != 3)
            {
                allTriangles = false;
            }

            currentFaceVertexCount = 0;
        }

        allTriangles &= currentFaceVertexCount == 0;

        if (faceCount == 0)
        {
            return new MeshTriangulationResult([], []);
        }

        int[] trianglesPerFace = new int[faceCount];

        if (allTriangles)
        {
            int[] triangleIndices = new int[faceCount * 3];
            int write = 0;
            int faceIndex = 0;

            for (int i = 0; i < polygonVertexIndices.Length; i++)
            {
                int encoded = polygonVertexIndices[i];
                triangleIndices[write++] = encoded < 0 ? -encoded - 1 : encoded;

                if (encoded < 0)
                {
                    trianglesPerFace[faceIndex++] = 1;
                }
            }

            return new MeshTriangulationResult(triangleIndices, trianglesPerFace);
        }

        List<int> vertices = [];
        List<int> outputTriangles = [];
        int outputFaceIndex = 0;

        for (int i = 0; i < polygonVertexIndices.Length; i++)
        {
            int encoded = polygonVertexIndices[i];
            int vertex = encoded < 0 ? -encoded - 1 : encoded;
            vertices.Add(vertex);

            if (encoded >= 0)
            {
                continue;
            }

            if (vertices.Count >= 3)
            {
                int generatedTriangles = vertices.Count - 2;
                trianglesPerFace[outputFaceIndex] = generatedTriangles;

                int first = vertices[0];
                for (int tri = 1; tri < vertices.Count - 1; tri++)
                {
                    outputTriangles.Add(first);
                    outputTriangles.Add(vertices[tri]);
                    outputTriangles.Add(vertices[tri + 1]);
                }
            }
            else
            {
                trianglesPerFace[outputFaceIndex] = 0;
            }

            outputFaceIndex++;
            vertices.Clear();
        }

        return new MeshTriangulationResult(outputTriangles.ToArray(), trianglesPerFace);
    }

    /// <summary>
    /// Triangulates a single planar or near-planar polygon, splitting quads along their shorter diagonal and ear clipping larger polygons.
    /// </summary>
    /// <param name="polygon">The polygon corner positions in winding order.</param>
    /// <param name="triangles">The destination for corner indices, which must hold at least <c>(polygon.Length - 2) * 3</c> values.</param>
    /// <returns>The number of indices written.</returns>
    public static int TriangulatePolygon(ReadOnlySpan<Vector3> polygon, Span<int> triangles)
    {
        if (polygon.Length < 3)
            return 0;

        if (polygon.Length == 3)
        {
            triangles[0] = 0;
            triangles[1] = 1;
            triangles[2] = 2;
            return 3;
        }

        if (polygon.Length == 4)
        {
            Vector3	quadNormal		= Vector3.Cross(polygon[2] - polygon[0], polygon[3] - polygon[1]);
            bool	splitsAtZero	= IsQuadDiagonalInside(polygon, 0, quadNormal);
            bool	splitsAtOne		= IsQuadDiagonalInside(polygon, 1, quadNormal);
            bool	zeroShorter		= Vector3.DistanceSquared(polygon[0], polygon[2]) <= Vector3.DistanceSquared(polygon[1], polygon[3]);
            int		start			= splitsAtZero && (!splitsAtOne || zeroShorter) ? 0 : 1;

            triangles[0] = start;
            triangles[1] = start + 1;
            triangles[2] = (start + 2) % 4;
            triangles[3] = start;
            triangles[4] = (start + 2) % 4;
            triangles[5] = (start + 3) % 4;
            return 6;
        }

        Vector3 normal = Vector3.Zero;

        for (int i = 0; i < polygon.Length; i++)
        {
            Vector3 current	= polygon[i];
            Vector3 next	= polygon[(i + 1) % polygon.Length];

            normal += new Vector3((current.Y - next.Y) * (current.Z + next.Z), (current.Z - next.Z) * (current.X + next.X), (current.X - next.X) * (current.Y + next.Y));
        }

        if (normal.LengthSquared() <= float.Epsilon)
            return TriangulateFan(polygon.Length, triangles);

        normal = Vector3.Normalize(normal);

        Vector3 axisX = Vector3.Normalize(Vector3.Cross(MathF.Abs(normal.X) > MathF.Abs(normal.Z) ? Vector3.UnitZ : Vector3.UnitX, normal));
        Vector3 axisY = Vector3.Cross(normal, axisX);

        Span<Vector2> points = polygon.Length <= 64 ? stackalloc Vector2[polygon.Length] : new Vector2[polygon.Length];

        for (int i = 0; i < polygon.Length; i++)
            points[i] = new Vector2(Vector3.Dot(polygon[i], axisX), Vector3.Dot(polygon[i], axisY));

        return new PolygonEarClipper(points).Triangulate(triangles);
    }

    /// <summary>
    /// Expands per-face values to per-triangle values using a triangle expansion map.
    /// </summary>
    /// <param name="perFaceValues">Values stored per source polygon face.</param>
    /// <param name="trianglesPerFace">Triangle counts per source polygon face.</param>
    /// <returns>Expanded values aligned to the triangle list.</returns>
    public static int[] ExpandPerFaceValuesToTriangles(ReadOnlySpan<int> perFaceValues, ReadOnlySpan<int> trianglesPerFace)
    {
        if (perFaceValues.Length == 0 || trianglesPerFace.Length == 0)
        {
            return [];
        }

        int expandedLength = 0;
        for (int i = 0; i < trianglesPerFace.Length; i++)
        {
            expandedLength += Math.Max(0, trianglesPerFace[i]);
        }

        if (expandedLength == 0)
        {
            return [];
        }

        int[] expanded = new int[expandedLength];
        int write = 0;

        for (int faceIndex = 0; faceIndex < trianglesPerFace.Length; faceIndex++)
        {
            int sourceValue = perFaceValues[Math.Min(faceIndex, perFaceValues.Length - 1)];
            int triangleCount = Math.Max(0, trianglesPerFace[faceIndex]);

            for (int i = 0; i < triangleCount; i++)
            {
                expanded[write++] = sourceValue;
            }
        }

        return expanded;
    }

    private static bool IsQuadDiagonalInside(ReadOnlySpan<Vector3> quad, int start, Vector3 normal)
    {
        Vector3 a = quad[start];
        Vector3 b = quad[start + 1];
        Vector3 c = quad[(start + 2) % 4];
        Vector3 d = quad[(start + 3) % 4];

        return Vector3.Dot(Vector3.Cross(b - a, c - a), normal) > 0.0f && Vector3.Dot(Vector3.Cross(c - a, d - a), normal) > 0.0f;
    }

    private static int TriangulateFan(int cornerCount, Span<int> triangles)
    {
        int written = 0;

        for (int i = 1; i < cornerCount - 1; i++)
        {
            triangles[written++] = 0;
            triangles[written++] = i;
            triangles[written++] = i + 1;
        }

        return written;
    }
}
