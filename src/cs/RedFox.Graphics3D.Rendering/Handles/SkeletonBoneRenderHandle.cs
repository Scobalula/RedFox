using System;
using System.Numerics;
using RedFox.Graphics3D.Rendering;
using RedFox.Graphics3D.Rendering.Materials;

namespace RedFox.Graphics3D.Rendering.Handles;

/// <summary>
/// Owns skeleton-bone overlay geometry and pipeline state for backend rendering.
/// </summary>
internal sealed class SkeletonBoneRenderHandle : RenderHandle
{
    internal const int FloatCountPerVertex = 13;
    private const float MinimumLineWidth = 0.75f;
    internal const string SkeletonMaterialTypeName = "Skeleton";

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
    private float _lastLineWidth;
    private float[] _lineVertices = [];
    private float[] _worldVertices = [];
    private long _transformVersion = -1;
    private bool _showBones = true;
    private int _vertexCount = -1;
    private SkeletonBoneBatch? _batch;

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
    public override bool RequiresPerFrameUpdate => true;

    /// <inheritdoc/>
    public override void Update(ICommandList commandList)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(commandList);

        _showBones = _bone.ShowSkeletonBone;
        if (!_showBones)
        {
            _vertexCount = -1;
            return;
        }

        _batch = SceneRenderResources.GetOrCreate(_graphicsDevice, typeof(SkeletonBoneBatch), () => new SkeletonBoneBatch(_graphicsDevice, _materialTypes));
        _batch.Update(commandList);

        long transformVersion = Transform.Version;
        if (_transformVersion != transformVersion || IsStyleStale())
        {
            Matrix4x4 localMatrix = _bone.GetActiveLocalMatrix();
            bool hasParent = _bone.Parent is not null;
            if (IsGeometryStale(localMatrix, hasParent))
            {
                _vertexCount = BuildLineVertices(_bone, localMatrix, hasParent).Length / FloatCountPerVertex;
                UpdateCachedState(localMatrix, hasParent);
            }

            UpdateWorldVertices(_bone.GetActiveWorldMatrix(), MathF.Max(_bone.BoneLineWidth, MinimumLineWidth) * 0.5f);
            _transformVersion = transformVersion;
        }

        _batch.Append(ResolveRenderPhase(), _worldVertices.AsSpan(0, _vertexCount * FloatCountPerVertex));
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
        _lastLineWidth = _bone.BoneLineWidth;
    }

    /// <inheritdoc/>
    public override void Render(ICommandList commandList, RenderFlags phase, in Matrix4x4 view, in Matrix4x4 projection, in Matrix4x4 sceneAxis, Vector3 cameraPosition, Vector2 viewportSize)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(commandList);

        if (phase != ResolveRenderPhase() || !_showBones || _batch is null)
        {
            return;
        }

        _batch.Render(commandList, phase, view, projection, sceneAxis, cameraPosition, viewportSize);
    }

    /// <inheritdoc/>
    protected override void ReleaseResources()
    {
        _batch = null;
        _vertexCount = -1;
    }

    private bool IsGeometryStale(Matrix4x4 localMatrix, bool hasParent)
    {
        return _lastLocalMatrix != localMatrix || _lastHasParent != hasParent || IsStyleStale();
    }

    private bool IsStyleStale()
    {
        return _vertexCount < 0
            || _lastLineWidth != _bone.BoneLineWidth
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

    private void UpdateWorldVertices(Matrix4x4 worldMatrix, float lineHalfWidth)
    {
        int floatCount = _vertexCount * FloatCountPerVertex;
        if (_worldVertices.Length < floatCount)
            _worldVertices = new float[floatCount];

        Span<float> vertices = _worldVertices.AsSpan(0, floatCount);
        _lineVertices.AsSpan(0, floatCount).CopyTo(vertices);

        for (int offset = 0; offset < floatCount; offset += FloatCountPerVertex)
        {
            Span<float> vertex = vertices.Slice(offset, FloatCountPerVertex);
            Vector3.Transform(new Vector3(vertex[..3]), worldMatrix).CopyTo(vertex);
            Vector3.Transform(new Vector3(vertex[3..6]), worldMatrix).CopyTo(vertex[3..]);
            vertex[12] *= lineHalfWidth;
        }
    }

    private RenderFlags ResolveRenderPhase()
    {
        return _bone.RenderBoneOnTop ? RenderFlags.Overlay : RenderFlags.Transparent;
    }
}
