using System.Numerics;

namespace RedFox.Graphics3D.Avalonia;

/// <summary>
/// Identifies a clickable part of a view cube: a face has one non-zero component, an edge two and a corner three.
/// </summary>
/// <param name="X">The sign along the cube's right axis.</param>
/// <param name="Y">The sign along the cube's up axis.</param>
/// <param name="Z">The sign along the cube's front axis.</param>
public readonly record struct ViewCubeRegion(int X, int Y, int Z)
{
    /// <summary>
    /// Gets every region of the cube.
    /// </summary>
    public static IReadOnlyList<ViewCubeRegion> All { get; } = CreateAll();

    /// <summary>
    /// Gets the direction from the cube's centre to this region.
    /// </summary>
    public Vector3 Direction => Vector3.Normalize(new Vector3(X, Y, Z));

    private static List<ViewCubeRegion> CreateAll()
    {
        List<ViewCubeRegion> regions = [];

        foreach (int x in new[] { -1, 0, 1 })
        {
            foreach (int y in new[] { -1, 0, 1 })
            {
                foreach (int z in new[] { -1, 0, 1 })
                {
                    if (x != 0 || y != 0 || z != 0)
                    {
                        regions.Add(new ViewCubeRegion(x, y, z));
                    }
                }
            }
        }

        return regions;
    }
}
