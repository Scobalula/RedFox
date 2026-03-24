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
}
