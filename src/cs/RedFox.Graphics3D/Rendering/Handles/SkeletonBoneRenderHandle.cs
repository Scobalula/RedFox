using System;
using System.Collections.Generic;
using System.Numerics;
using RedFox.Graphics3D.Rendering;
using RedFox.Graphics3D.Rendering.Materials;

namespace RedFox.Graphics3D.Rendering.Handles;

/// <summary>
/// Owns skeleton-bone overlay geometry and pipeline state for backend rendering.
/// </summary>
internal sealed class SkeletonBoneRenderHandle : RenderHandle
{
    private const int FloatCountPerVertex = SkeletonBoneRenderBatch.FloatCountPerVertex;
    private static readonly object SharedBatchLock = new();
    private static readonly Dictionary<IGraphicsDevice, (SkeletonBoneRenderBatch Batch, int ReferenceCount)> SharedBatches = [];

    private readonly SkeletonBone _bone;
    private readonly IGraphicsDevice _graphicsDevice;
    private readonly IMaterialTypeRegistry _materialTypes;

    private float _lastAxisMaxSize;
    private float _lastAxisScaleFromParent;
    private float _lastAxisSize;
    private Vector4 _lastBoneAxisXColor;
    private Vector4 _lastBoneAxisYColor;
    private Vector4 _lastBoneAxisZColor;
    private Vector4 _lastBoneConnectionColor;
    private float _lastBoneNameColorSaturation;
    private float _lastBoneNameColorValue;
    private bool _lastHasParent;
    private Matrix4x4 _lastLocalMatrix;
    private bool _lastUseBoneNameHashColor = true;
    private SkeletonBoneRenderBatch? _batch;
    private float[] _lineVertices = [];
    private bool _ownsBatchReference;
    private RenderPhaseMask _renderPhases;
    private bool _showBones = true;
    private int _vertexCount = -1;
    private Matrix4x4 _worldMatrix;

    /// <summary>
    /// Initializes a new instance of the <see cref="SkeletonBoneRenderHandle"/> class.
    /// </summary>
    /// <param name="graphicsDevice">The graphics device that owns overlay resources.</param>
    /// <param name="materialTypes">The material registry used to resolve the skeleton pipeline.</param>
    /// <param name="bone">The skeleton bone node represented by this handle.</param>
    public SkeletonBoneRenderHandle(IGraphicsDevice graphicsDevice, IMaterialTypeRegistry materialTypes, SkeletonBone bone)
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        _materialTypes = materialTypes ?? throw new ArgumentNullException(nameof(materialTypes));
        _bone = bone ?? throw new ArgumentNullException(nameof(bone));
    }

    /// <inheritdoc/>
    public override RenderPhaseMask RenderPhases => _renderPhases;

    /// <inheritdoc/>
    public override bool RequiresPerFrameUpdate => true;

    /// <inheritdoc/>
    public override void Update(ICommandList commandList)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(commandList);

        _renderPhases = RenderPhaseMask.None;
        _showBones = _bone.ShowSkeletonBone;
        if (!_showBones)
        {
            _vertexCount = -1;
            return;
        }

        _worldMatrix = _bone.GetActiveWorldMatrix();
        EnsureBatch();

        Matrix4x4 localMatrix = _bone.GetActiveLocalMatrix();
        bool hasParent = _bone.Parent is not null;
        ReadOnlySpan<float> vertices;
        if (IsGeometryStale(localMatrix, hasParent))
        {
            vertices = BuildLineVertices(_bone, localMatrix, hasParent);
            _vertexCount = vertices.Length / FloatCountPerVertex;
            UpdateCachedState(localMatrix, hasParent);
        }
        else
        {
            vertices = _lineVertices.AsSpan(0, Math.Max(0, _vertexCount) * FloatCountPerVertex);
        }

        if (vertices.IsEmpty)
        {
            UpdateRenderPhases();
            return;
        }

        RenderPhaseMask renderPhase = _bone.RenderBoneOnTop ? RenderPhaseMask.Overlay : RenderPhaseMask.Transparent;
        _batch?.Queue(commandList, vertices, _worldMatrix, renderPhase, MathF.Max(_bone.BoneLineWidth, 0.75f) * 0.5f);
        UpdateRenderPhases();
    }

    private void UpdateCachedState(Matrix4x4 localMatrix, bool hasParent)
    {
        _lastLocalMatrix = localMatrix;
        _lastHasParent = hasParent;
        _lastAxisSize = _bone.BoneAxisSize;
        _lastAxisScaleFromParent = _bone.BoneAxisScaleFromParent;
        _lastAxisMaxSize = _bone.BoneAxisMaxSize;
        _lastBoneAxisXColor = _bone.BoneAxisXColor;
        _lastBoneAxisYColor = _bone.BoneAxisYColor;
        _lastBoneAxisZColor = _bone.BoneAxisZColor;
        _lastBoneConnectionColor = _bone.BoneConnectionColor;
        _lastUseBoneNameHashColor = _bone.UseBoneNameHashColor;
        _lastBoneNameColorSaturation = _bone.BoneNameColorSaturation;
        _lastBoneNameColorValue = _bone.BoneNameColorValue;
    }

    /// <inheritdoc/>
    public override void Render(
        ICommandList commandList,
        RenderPhase phase,
        in Matrix4x4 view,
        in Matrix4x4 projection,
        in Matrix4x4 sceneAxis,
        Vector3 cameraPosition,
        Vector2 viewportSize)
    {
        ThrowIfDisposed();

        RenderPhase targetPhase = _bone.RenderBoneOnTop ? RenderPhase.Overlay : RenderPhase.Transparent;
        if (phase != targetPhase || !_showBones || _batch is null || _vertexCount <= 0)
        {
            return;
        }

        _batch.Render(commandList, phase, view, projection, sceneAxis, cameraPosition, viewportSize);
    }

    /// <inheritdoc/>
    protected override void ReleaseCore()
    {
        ReleaseBatchReference();
        _vertexCount = -1;
        _renderPhases = RenderPhaseMask.None;
    }

    private void UpdateRenderPhases()
    {
        if (!_showBones || _batch is null || _vertexCount <= 0)
        {
            _renderPhases = RenderPhaseMask.None;
            return;
        }

        _renderPhases = _bone.RenderBoneOnTop ? RenderPhaseMask.Overlay : RenderPhaseMask.Transparent;
    }

    private bool IsGeometryStale(Matrix4x4 localMatrix, bool hasParent)
    {
        return _vertexCount < 0
            || _lastLocalMatrix != localMatrix
            || _lastHasParent != hasParent
            || _lastAxisSize != _bone.BoneAxisSize
            || _lastAxisScaleFromParent != _bone.BoneAxisScaleFromParent
            || _lastAxisMaxSize != _bone.BoneAxisMaxSize
            || _lastBoneAxisXColor != _bone.BoneAxisXColor
            || _lastBoneAxisYColor != _bone.BoneAxisYColor
            || _lastBoneAxisZColor != _bone.BoneAxisZColor
            || _lastBoneConnectionColor != _bone.BoneConnectionColor
            || _lastUseBoneNameHashColor != _bone.UseBoneNameHashColor
            || _lastBoneNameColorSaturation != _bone.BoneNameColorSaturation
            || _lastBoneNameColorValue != _bone.BoneNameColorValue;
    }

    private static void AddExpandedVertex(Span<float> destination, ref int offset, Vector3 start, Vector3 end, Vector4 color, float along, float side, float widthScale)
    {
        destination[offset++] = start.X;
        destination[offset++] = start.Y;
        destination[offset++] = start.Z;
        destination[offset++] = end.X;
        destination[offset++] = end.Y;
        destination[offset++] = end.Z;
        destination[offset++] = color.X;
        destination[offset++] = color.Y;
        destination[offset++] = color.Z;
        destination[offset++] = color.W;
        destination[offset++] = along;
        destination[offset++] = side;
        destination[offset++] = widthScale;
    }

    private static void AddLineSegment(Span<float> destination, ref int offset, Vector3 start, Vector3 end, Vector4 color, float widthScale)
    {
        AddExpandedVertex(destination, ref offset, start, end, color, 0.0f, -1.0f, widthScale);
        AddExpandedVertex(destination, ref offset, start, end, color, 0.0f, 1.0f, widthScale);
        AddExpandedVertex(destination, ref offset, start, end, color, 1.0f, 1.0f, widthScale);
        AddExpandedVertex(destination, ref offset, start, end, color, 0.0f, -1.0f, widthScale);
        AddExpandedVertex(destination, ref offset, start, end, color, 1.0f, 1.0f, widthScale);
        AddExpandedVertex(destination, ref offset, start, end, color, 1.0f, -1.0f, widthScale);
    }

    private ReadOnlySpan<float> BuildLineVertices(SkeletonBone bone, Matrix4x4 localMatrix, bool hasParent)
    {
        float axisSize = MathF.Max(bone.BoneAxisSize, 0.0f);
        Matrix4x4 inverseLocal = Matrix4x4.Identity;
        hasParent = hasParent && Matrix4x4.Invert(localMatrix, out inverseLocal);
        Vector3 parentLocalOrigin = Vector3.Zero;
        if (hasParent)
        {
            parentLocalOrigin = Vector3.Transform(Vector3.Zero, inverseLocal);
            float parentDistance = parentLocalOrigin.Length();
            if (parentDistance > 1e-6f)
            {
                float scaledFromParent = parentDistance * MathF.Max(bone.BoneAxisScaleFromParent, 0.0f);
                axisSize = MathF.Max(axisSize, scaledFromParent);
            }
        }

        if (bone.BoneAxisMaxSize > 0.0f)
        {
            axisSize = MathF.Min(axisSize, bone.BoneAxisMaxSize);
        }

        int segmentCount = axisSize > 0.0f ? 3 : 0;
        if (hasParent)
        {
            segmentCount++;
        }

        int floatCount = segmentCount * 6 * FloatCountPerVertex;
        if (floatCount == 0)
        {
            return ReadOnlySpan<float>.Empty;
        }

        if (_lineVertices.Length < floatCount)
        {
            _lineVertices = new float[floatCount];
        }

        Span<float> vertices = _lineVertices.AsSpan(0, floatCount);
        int offset = 0;
        if (axisSize > 0.0f)
        {
            AddLineSegment(vertices, ref offset, Vector3.Zero, Vector3.UnitX * axisSize, bone.BoneAxisXColor, 1.0f);
            AddLineSegment(vertices, ref offset, Vector3.Zero, Vector3.UnitY * axisSize, bone.BoneAxisYColor, 1.0f);
            AddLineSegment(vertices, ref offset, Vector3.Zero, Vector3.UnitZ * axisSize, bone.BoneAxisZColor, 1.0f);
        }

        if (hasParent)
        {
            Vector4 connectionColor = bone.UseBoneNameHashColor
                ? BuildBoneNameColor(bone)
                : bone.BoneConnectionColor;
            AddLineSegment(vertices, ref offset, parentLocalOrigin, Vector3.Zero, connectionColor, 1.0f);
        }

        return vertices;
    }

    private static Vector4 BuildBoneNameColor(SkeletonBone bone)
    {
        string source = string.IsNullOrWhiteSpace(bone.Name) ? "Bone" : bone.Name;
        uint hash = 2166136261u;
        for (int i = 0; i < source.Length; i++)
        {
            hash ^= source[i];
            hash *= 16777619u;
        }

        float hue = (hash % 360u) / 360.0f;
        float saturation = Math.Clamp(bone.BoneNameColorSaturation, 0.0f, 1.0f);
        float value = Math.Clamp(bone.BoneNameColorValue, 0.0f, 1.0f);
        Vector3 rgb = HsvToRgb(hue, saturation, value);
        return new Vector4(rgb.X, rgb.Y, rgb.Z, bone.BoneConnectionColor.W);
    }

    private static Vector3 HsvToRgb(float hue, float saturation, float value)
    {
        if (saturation <= 0.0f)
        {
            return new Vector3(value, value, value);
        }

        float scaledHue = (hue - MathF.Floor(hue)) * 6.0f;
        int sextant = (int)scaledHue;
        float fraction = scaledHue - sextant;
        float p = value * (1.0f - saturation);
        float q = value * (1.0f - saturation * fraction);
        float t = value * (1.0f - saturation * (1.0f - fraction));

        return sextant switch
        {
            0 => new Vector3(value, t, p),
            1 => new Vector3(q, value, p),
            2 => new Vector3(p, value, t),
            3 => new Vector3(p, q, value),
            4 => new Vector3(t, p, value),
            _ => new Vector3(value, p, q),
        };
    }

    private void EnsureBatch()
    {
        if (_batch is not null)
        {
            return;
        }

        lock (SharedBatchLock)
        {
            if (SharedBatches.TryGetValue(_graphicsDevice, out (SkeletonBoneRenderBatch Batch, int ReferenceCount) sharedBatch))
            {
                SharedBatches[_graphicsDevice] = (sharedBatch.Batch, sharedBatch.ReferenceCount + 1);
                _batch = sharedBatch.Batch;
                _ownsBatchReference = true;
                return;
            }

            SkeletonBoneRenderBatch batch = new(_graphicsDevice, _materialTypes);
            SharedBatches.Add(_graphicsDevice, (batch, 1));
            _batch = batch;
            _ownsBatchReference = true;
        }
    }

    private void ReleaseBatchReference()
    {
        if (!_ownsBatchReference || _batch is null)
        {
            return;
        }

        lock (SharedBatchLock)
        {
            if (SharedBatches.TryGetValue(_graphicsDevice, out (SkeletonBoneRenderBatch Batch, int ReferenceCount) sharedBatch))
            {
                int referenceCount = sharedBatch.ReferenceCount - 1;
                if (referenceCount <= 0)
                {
                    SharedBatches.Remove(_graphicsDevice);
                    sharedBatch.Batch.Dispose();
                }
                else
                {
                    SharedBatches[_graphicsDevice] = (sharedBatch.Batch, referenceCount);
                }
            }

            _batch = null;
            _ownsBatchReference = false;
        }
    }
}