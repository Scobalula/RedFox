using System;
using System.Numerics;

namespace RedFox.Rendering.OpenGL;

/// <summary>
/// Frame-scoped light state collected by <see cref="Passes.OpenGlSceneCollectionPass"/>.
/// </summary>
internal sealed class OpenGlLightFrameData
{
    public const int MaxSceneLights = 4;

    private readonly Vector4[] _directionsAndIntensity = new Vector4[MaxSceneLights];
    private readonly Vector3[] _colors = new Vector3[MaxSceneLights];

    public int Count { get; private set; }

    public ReadOnlySpan<Vector4> DirectionsAndIntensity => _directionsAndIntensity;

    public ReadOnlySpan<Vector3> Colors => _colors;

    public void Reset()
    {
        Count = 0;
        Array.Clear(_directionsAndIntensity);
        Array.Clear(_colors);
    }

    public void Add(Vector3 direction, float intensity, Vector3 color)
    {
        if (Count >= MaxSceneLights)
        {
            return;
        }

        _directionsAndIntensity[Count] = new Vector4(direction, intensity);
        _colors[Count] = color;
        Count++;
    }
}
