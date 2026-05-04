using System;
using System.Numerics;
using System.Runtime.InteropServices;
using RedFox.Graphics3D.Rendering;
using RedFox.Graphics3D.Rendering.Materials;

namespace RedFox.Graphics3D.Rendering.Handles;

/// <summary>
/// Owns skeleton-bone overlay geometry and pipeline state for backend rendering.
/// </summary>
internal sealed class SkeletonBoneRenderHandle : RenderHandle
{
    private const int FloatCountPerVertex = 13;
    private const int VertexStrideBytes = FloatCountPerVertex * sizeof(float);
    private const float MinimumLineWidth = 0.75f;
    private const string SkeletonMaterialTypeName = "Skeleton";

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
    private float[] _lineVertices = [];
    private IGpuPipelineState? _pipeline;
    private bool _showBones = true;
    private IGpuBuffer? _vertexBuffer;
    private int _vertexBufferSizeBytes;
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

        _worldMatrix = _bone.GetActiveWorldMatrix();
        EnsurePipeline();

        Matrix4x4 localMatrix = _bone.GetActiveLocalMatrix();
        bool hasParent = _bone.Parent is not null;
        if (IsGeometryStale(localMatrix, hasParent))
        {
            ReadOnlySpan<float> vertices = BuildLineVertices(_bone, localMatrix, hasParent);
            _vertexCount = vertices.Length / FloatCountPerVertex;
            UpdateCachedState(localMatrix, hasParent);
            UploadVertices(vertices);
            return;
        }

        if (_vertexCount > 0 && (_vertexBuffer is null || _vertexBuffer.IsDisposed))
        {
            UploadVertices(_lineVertices.AsSpan(0, _vertexCount * FloatCountPerVertex));
        }
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
        RenderFlags phase,
        in Matrix4x4 view,
        in Matrix4x4 projection,
        in Matrix4x4 sceneAxis,
        Vector3 cameraPosition,
        Vector2 viewportSize)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(commandList);

        RenderFlags targetPhase = ResolveRenderPhase();
        if (phase != targetPhase || !_showBones || _pipeline is null || _vertexBuffer is null || _vertexCount <= 0)
        {
            return;
        }

        commandList.SetPipelineState(_pipeline);
        BindVertexBuffer(commandList, _pipeline, _vertexBuffer);
        commandList.SetUniformMatrix4x4("Model", _worldMatrix);
        commandList.SetUniformMatrix4x4("View", view);
        commandList.SetUniformMatrix4x4("Projection", projection);
        commandList.SetUniformMatrix4x4("SceneAxis", sceneAxis);
        commandList.SetUniformVector3("CameraPosition", cameraPosition);
        commandList.SetUniformVector2("ViewportSize", viewportSize);
        commandList.SetUniformFloat("LineHalfWidthPx", MathF.Max(_bone.BoneLineWidth, MinimumLineWidth) * 0.5f);
        commandList.Draw(_vertexCount, 0);
    }

    /// <inheritdoc/>
    protected override void ReleaseCore()
    {
        _vertexBuffer?.Dispose();
        _vertexBuffer = null;
        _vertexBufferSizeBytes = 0;
        ReleasePipeline();
        _vertexCount = -1;
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

    private static void BindVertexAttribute(ICommandList commandList, IGpuPipelineState pipeline, IGpuBuffer vertexBuffer, string attributeName)
    {
        if (!pipeline.TryGetBufferSlot(attributeName, out int slot))
        {
            return;
        }

        commandList.BindBuffer(slot, vertexBuffer);
    }

    private static void BindVertexBuffer(ICommandList commandList, IGpuPipelineState pipeline, IGpuBuffer vertexBuffer)
    {
        BindVertexAttribute(commandList, pipeline, vertexBuffer, "LineStart");
        BindVertexAttribute(commandList, pipeline, vertexBuffer, "LineEnd");
        BindVertexAttribute(commandList, pipeline, vertexBuffer, "Color");
        BindVertexAttribute(commandList, pipeline, vertexBuffer, "Along");
        BindVertexAttribute(commandList, pipeline, vertexBuffer, "Side");
        BindVertexAttribute(commandList, pipeline, vertexBuffer, "WidthScale");
    }

    private RenderFlags ResolveRenderPhase()
    {
        return _bone.RenderBoneOnTop ? RenderFlags.Overlay : RenderFlags.Transparent;
    }

    private void EnsurePipeline()
    {
        if (_pipeline is not null && !_pipeline.IsDisposed)
        {
            return;
        }

        if (_materialTypes is not IMaterialPipelineProvider pipelineProvider)
        {
            throw new InvalidOperationException($"Material registry '{_materialTypes.GetType().Name}' does not provide runtime pipeline services.");
        }

        _pipeline = pipelineProvider.AcquirePipeline(_graphicsDevice, SkeletonMaterialTypeName);
    }

    private void ReleasePipeline()
    {
        IGpuPipelineState? pipeline = _pipeline;
        _pipeline = null;

        if (pipeline is null)
        {
            return;
        }

        if (_materialTypes is IMaterialPipelineProvider pipelineProvider)
        {
            pipelineProvider.ReleasePipeline(SkeletonMaterialTypeName, pipeline);
            return;
        }

        pipeline.Dispose();
    }

    private void UploadVertices(ReadOnlySpan<float> vertices)
    {
        if (vertices.IsEmpty)
        {
            _vertexBuffer?.Dispose();
            _vertexBuffer = null;
            _vertexBufferSizeBytes = 0;
            return;
        }

        ReadOnlySpan<byte> vertexBytes = MemoryMarshal.AsBytes(vertices);
        int sizeBytes = vertexBytes.Length;
        if (_vertexBuffer is null || _vertexBuffer.IsDisposed || _vertexBufferSizeBytes < sizeBytes)
        {
            _vertexBuffer?.Dispose();
            _vertexBuffer = _graphicsDevice.CreateBuffer(sizeBytes, VertexStrideBytes, BufferUsage.Vertex | BufferUsage.DynamicWrite, GpuBufferElementType.Float32, vertexBytes);
            _vertexBufferSizeBytes = sizeBytes;
            return;
        }

        _graphicsDevice.UpdateBuffer(_vertexBuffer, vertexBytes);
    }
}