using RedFox.Graphics3D.Buffers;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace RedFox.Graphics3D;

/// <summary>
/// Represents a morph deformer that offsets mesh vertices by weighted target deltas.
/// The value index of each delta buffer maps to the matching entry in <see cref="TargetNames"/> and <see cref="Weights"/>.
/// </summary>
public class Morph
{
    private readonly string[] _targetNames;

    private float[]? _maxPositionDeltaLengths;

    /// <summary>
    /// Gets or sets the name of the morph.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets the target names, aligned with the delta buffer value index.
    /// </summary>
    public IReadOnlyList<string> TargetNames => _targetNames;

    /// <summary>
    /// Gets the current target weights, aligned with <see cref="TargetNames"/>.
    /// A weight of 0 leaves the base mesh untouched and 1 applies the full delta.
    /// </summary>
    public float[] Weights { get; }

    /// <summary>
    /// Gets the per-vertex position deltas, or <see langword="null"/> when the morph does not affect positions.
    /// </summary>
    public DataBuffer? DeltaPositions { get; }

    /// <summary>
    /// Gets the per-vertex normal deltas, or <see langword="null"/> when the morph does not affect normals.
    /// </summary>
    public DataBuffer? DeltaNormals { get; }

    /// <summary>
    /// Gets the per-vertex tangent deltas, or <see langword="null"/> when the morph does not affect tangents.
    /// </summary>
    public DataBuffer? DeltaTangents { get; }

    /// <summary>
    /// Gets the number of morph targets.
    /// </summary>
    public int TargetCount => _targetNames.Length;

    /// <summary>
    /// Gets the number of vertices described by the delta buffers.
    /// </summary>
    public int VertexCount => DeltaPositions?.ElementCount ?? DeltaNormals?.ElementCount ?? DeltaTangents?.ElementCount ?? 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="Morph"/> class that only affects positions.
    /// </summary>
    /// <param name="targetNames">The target names aligned with the delta buffer value index.</param>
    /// <param name="deltaPositions">The per-vertex position deltas.</param>
    public Morph(IReadOnlyList<string> targetNames, DataBuffer deltaPositions) : this(targetNames, deltaPositions, null, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Morph"/> class.
    /// </summary>
    /// <param name="targetNames">The target names aligned with the delta buffer value index.</param>
    /// <param name="deltaPositions">The per-vertex position deltas, or <see langword="null"/>.</param>
    /// <param name="deltaNormals">The per-vertex normal deltas, or <see langword="null"/>.</param>
    /// <param name="deltaTangents">The per-vertex tangent deltas, or <see langword="null"/>.</param>
    /// <exception cref="ArgumentException">A delta buffer does not match the target count or the other buffers' vertex count.</exception>
    public Morph(IReadOnlyList<string> targetNames, DataBuffer? deltaPositions, DataBuffer? deltaNormals, DataBuffer? deltaTangents)
    {
        _targetNames = [.. targetNames];
        Weights = new float[_targetNames.Length];
        DeltaPositions = deltaPositions;
        DeltaNormals = deltaNormals;
        DeltaTangents = deltaTangents;

        Validate(deltaPositions, nameof(deltaPositions));
        Validate(deltaNormals, nameof(deltaNormals));
        Validate(deltaTangents, nameof(deltaTangents));
    }

    /// <summary>
    /// Gets the index of the target with the specified name.
    /// </summary>
    /// <param name="targetName">The target name.</param>
    /// <returns>The target index, or -1 when no target has that name.</returns>
    public int IndexOfTarget(string targetName) => Array.IndexOf(_targetNames, targetName);

    /// <summary>
    /// Gets the position offset of the specified vertex for the current <see cref="Weights"/>.
    /// </summary>
    /// <param name="vertexIndex">The zero-based vertex index.</param>
    /// <returns>The weighted sum of the position deltas, or zero when the morph does not affect positions.</returns>
    public Vector3 GetPositionDelta(int vertexIndex) => GetWeightedDelta(DeltaPositions, vertexIndex);

    /// <summary>
    /// Gets the normal offset of the specified vertex for the current <see cref="Weights"/>.
    /// </summary>
    /// <param name="vertexIndex">The zero-based vertex index.</param>
    /// <returns>The weighted sum of the normal deltas, or zero when the morph does not affect normals.</returns>
    public Vector3 GetNormalDelta(int vertexIndex) => GetWeightedDelta(DeltaNormals, vertexIndex);

    /// <summary>
    /// Gets the tangent offset of the specified vertex for the current <see cref="Weights"/>.
    /// </summary>
    /// <param name="vertexIndex">The zero-based vertex index.</param>
    /// <returns>The weighted sum of the tangent deltas, or zero when the morph does not affect tangents.</returns>
    public Vector3 GetTangentDelta(int vertexIndex) => GetWeightedDelta(DeltaTangents, vertexIndex);

    /// <summary>
    /// Gets an upper bound on how far any vertex can move from its base position for the current <see cref="Weights"/>.
    /// </summary>
    /// <returns>The maximum position offset length.</returns>
    public float GetMaxPositionOffset()
    {
        if (DeltaPositions is null)
            return 0f;

        _maxPositionDeltaLengths ??= GetMaxDeltaLengths(DeltaPositions);

        float offset = 0f;

        for (int targetIndex = 0; targetIndex < _targetNames.Length; targetIndex++)
            offset += MathF.Abs(Weights[targetIndex]) * _maxPositionDeltaLengths[targetIndex];

        return offset;
    }

    /// <summary>
    /// Creates a copy of this morph that shares its buffers and target names but owns its weights.
    /// </summary>
    /// <returns>The copied morph.</returns>
    public Morph Clone()
    {
        Morph clone = new(_targetNames, DeltaPositions, DeltaNormals, DeltaTangents) { Name = Name };
        Weights.CopyTo(clone.Weights);
        return clone;
    }

    private Vector3 GetWeightedDelta(DataBuffer? deltas, int vertexIndex)
    {
        Vector3 result = Vector3.Zero;

        if (deltas is null)
            return result;

        for (int targetIndex = 0; targetIndex < _targetNames.Length; targetIndex++)
        {
            float weight = Weights[targetIndex];

            if (weight != 0f)
                result += deltas.GetVector3(vertexIndex, targetIndex) * weight;
        }

        return result;
    }

    private float[] GetMaxDeltaLengths(DataBuffer deltas)
    {
        float[] lengths = new float[_targetNames.Length];

        for (int vertexIndex = 0; vertexIndex < deltas.ElementCount; vertexIndex++)
        {
            for (int targetIndex = 0; targetIndex < lengths.Length; targetIndex++)
                lengths[targetIndex] = MathF.Max(lengths[targetIndex], deltas.GetVector3(vertexIndex, targetIndex).Length());
        }

        return lengths;
    }

    private void Validate(DataBuffer? buffer, string parameterName)
    {
        if (buffer is null)
            return;

        if (buffer.ValueCount != _targetNames.Length)
            throw new ArgumentException($"Expected {_targetNames.Length} morph targets but the buffer contains {buffer.ValueCount}.", parameterName);

        if (buffer.ElementCount != VertexCount)
            throw new ArgumentException($"Expected {VertexCount} vertices but the buffer contains {buffer.ElementCount}.", parameterName);
    }
}
