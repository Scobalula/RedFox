using System.Numerics;

namespace RedFox.Graphics3D;

/// <summary>
/// Triangulates a simple counter-clockwise 2D polygon by ear clipping, using a uniform grid of reflex vertices so large polygons stay fast.
/// </summary>
internal sealed class PolygonEarClipper
{
    private readonly Vector2[]	_points;
    private readonly int[]		_previous;
    private readonly int[]		_next;
    private readonly bool[]		_removed;
    private readonly int[]		_cellHeads;
    private readonly int[]		_cellNext;
    private readonly Vector2	_min;
    private readonly Vector2	_cellScale;
    private readonly int		_cells;

    /// <summary>
    /// Initializes a new <see cref="PolygonEarClipper"/>.
    /// </summary>
    /// <param name="points">The polygon corners in counter-clockwise order.</param>
    public PolygonEarClipper(ReadOnlySpan<Vector2> points)
    {
        int count = points.Length;

        _points		= points.ToArray();
        _previous	= new int[count];
        _next		= new int[count];
        _removed	= new bool[count];
        _cells		= Math.Clamp((int)MathF.Sqrt(count), 1, 256);
        _cellHeads	= new int[_cells * _cells];
        _cellNext	= new int[count];

        Vector2 min = points[0];
        Vector2 max = points[0];

        for (int i = 0; i < count; i++)
        {
            _previous[i]	= (i + count - 1) % count;
            _next[i]		= (i + 1) % count;
            min				= Vector2.Min(min, points[i]);
            max				= Vector2.Max(max, points[i]);
        }

        Vector2 size = max - min;

        _min		= min;
        _cellScale	= new Vector2(size.X > 0.0f ? _cells / size.X : 0.0f, size.Y > 0.0f ? _cells / size.Y : 0.0f);

        Array.Fill(_cellHeads, -1);

        for (int i = 0; i < count; i++)
        {
            if (!IsReflex(i))
                continue;

            int cell = GetCell(points[i]);

            _cellNext[i]		= _cellHeads[cell];
            _cellHeads[cell]	= i;
        }
    }

    /// <summary>
    /// Triangulates the polygon.
    /// </summary>
    /// <param name="triangles">The destination for corner indices, which must hold <c>(count - 2) * 3</c> values.</param>
    /// <returns>The number of indices written.</returns>
    public int Triangulate(Span<int> triangles)
    {
        int remaining	= _points.Length;
        int current		= 0;
        int stalled		= 0;
        int written		= 0;

        while (remaining > 3)
        {
            int previous	= _previous[current];
            int next		= _next[current];

            if (stalled < remaining && !IsEar(previous, current, next))
            {
                current = next;
                stalled++;
                continue;
            }

            triangles[written++] = previous;
            triangles[written++] = current;
            triangles[written++] = next;

            _next[previous]		= next;
            _previous[next]		= previous;
            _removed[current]	= true;

            remaining--;
            stalled = 0;
            current = previous;
        }

        triangles[written++] = _previous[current];
        triangles[written++] = current;
        triangles[written++] = _next[current];
        return written;
    }

    private static float Cross(Vector2 a, Vector2 b)
    {
        return a.X * b.Y - a.Y * b.X;
    }

    private bool IsReflex(int index)
    {
        Vector2 point = _points[index];
        return Cross(point - _points[_previous[index]], _points[_next[index]] - point) <= 0.0f;
    }

    private bool IsEar(int previous, int current, int next)
    {
        Vector2 a = _points[previous];
        Vector2 b = _points[current];
        Vector2 c = _points[next];

        if (Cross(b - a, c - b) <= 0.0f)
            return false;

        int minCell = GetCell(Vector2.Min(a, Vector2.Min(b, c)));
        int maxCell = GetCell(Vector2.Max(a, Vector2.Max(b, c)));

        for (int y = minCell / _cells; y <= maxCell / _cells; y++)
        {
            for (int x = minCell % _cells; x <= maxCell % _cells; x++)
            {
                for (int candidate = _cellHeads[y * _cells + x]; candidate >= 0; candidate = _cellNext[candidate])
                {
                    if (_removed[candidate] || candidate == previous || candidate == current || candidate == next || !IsReflex(candidate))
                        continue;

                    Vector2 point = _points[candidate];

                    if (Cross(b - a, point - a) > 0.0f && Cross(c - b, point - b) > 0.0f && Cross(a - c, point - c) > 0.0f)
                        return false;
                }
            }
        }

        return true;
    }

    private int GetCell(Vector2 point)
    {
        Vector2	cell	= (point - _min) * _cellScale;
        int		x		= Math.Clamp((int)cell.X, 0, _cells - 1);
        int		y		= Math.Clamp((int)cell.Y, 0, _cells - 1);

        return y * _cells + x;
    }
}
