using System;
using System.Numerics;
using System.Runtime.InteropServices;
using RedFox.Graphics3D.Rendering.Backend;
using RedFox.Graphics3D.Rendering.Materials;

namespace RedFox.Graphics3D.Rendering.Handles;

/// <summary>
/// Owns skeleton-bone overlay geometry and pipeline state for backend rendering.
/// </summary>
internal sealed class SkeletonBoneRenderHandle : RenderHandle
{
    private const int FloatCountPerVertex = 13;
    private const int LineAttributeSlotCount = 6;

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
    private IGpuBuffer? _lineBuffer;
    private int _lineBufferSizeBytes;
    private IGpuPipelineState? _pipeline;
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
    public override void Update(ICommandList commandList)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(commandList);

        SkeletonOverlay? overlay = _bone.Scene?.TryGetFirstOfType<SkeletonOverlay>();
        _showBones = overlay?.ShowSkeletonBones ?? true;
        _worldMatrix = _bone.GetActiveWorldMatrix();

        if (!_showBones)
        {
            _vertexCount = 0;
            return;
        }

        EnsurePipeline();

        Matrix4x4 localMatrix = _bone.GetActiveLocalMatrix();
        bool hasParent = _bone.Parent is not null;
        if (!IsGeometryStale(localMatrix, hasParent, overlay))
        {
            return;
        }

        float[] vertices = BuildLineVertices(_bone, overlay);
        _vertexCount = vertices.Length / FloatCountPerVertex;
        ReadOnlySpan<byte> vertexBytes = MemoryMarshal.AsBytes(vertices.AsSpan());
        if (_lineBuffer is null || _lineBufferSizeBytes != vertexBytes.Length)
        {
            _lineBuffer?.Dispose();
            _lineBuffer = _graphicsDevice.CreateBuffer(
                vertexBytes.Length,
                FloatCountPerVertex * sizeof(float),
                BufferUsage.Vertex | BufferUsage.DynamicWrite,
                vertexBytes);
            _lineBufferSizeBytes = vertexBytes.Length;
        }
        else
        {
            _graphicsDevice.UpdateBuffer(_lineBuffer, vertexBytes);
        }

        _lastLocalMatrix = localMatrix;
        _lastHasParent = hasParent;
        _lastAxisSize = overlay?.BoneAxisSize ?? 0.2f;
        _lastAxisScaleFromParent = overlay?.BoneAxisScaleFromParent ?? 0.2f;
        _lastAxisMaxSize = overlay?.BoneAxisMaxSize ?? 2.0f;
        _lastBoneAxisXColor = overlay?.BoneAxisXColor ?? new Vector4(0.9f, 0.25f, 0.25f, 0.95f);
        _lastBoneAxisYColor = overlay?.BoneAxisYColor ?? new Vector4(0.25f, 0.9f, 0.25f, 0.95f);
        _lastBoneAxisZColor = overlay?.BoneAxisZColor ?? new Vector4(0.25f, 0.45f, 0.95f, 0.95f);
        _lastBoneConnectionColor = overlay?.BoneConnectionColor ?? new Vector4(0.88f, 0.88f, 0.76f, 0.9f);
        _lastUseBoneNameHashColor = overlay?.UseBoneNameHashColor ?? true;
        _lastBoneNameColorSaturation = overlay?.BoneNameColorSaturation ?? 0.62f;
        _lastBoneNameColorValue = overlay?.BoneNameColorValue ?? 0.95f;
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

        if (phase != RenderPhase.Overlay || !_showBones || _pipeline is null || _lineBuffer is null || _vertexCount <= 0)
        {
            return;
        }

        SkeletonOverlay? overlay = _bone.Scene?.TryGetFirstOfType<SkeletonOverlay>();

        commandList.SetPipelineState(_pipeline);
        BindLineVertexBuffer(commandList, _lineBuffer);
        commandList.SetUniformMatrix4x4("Model", _worldMatrix);
        commandList.SetUniformMatrix4x4("SceneAxis", sceneAxis);
        commandList.SetUniformMatrix4x4("View", view);
        commandList.SetUniformMatrix4x4("Projection", projection);
        commandList.SetUniformVector2("ViewportSize", viewportSize);
        commandList.SetUniformFloat("LineHalfWidthPx", MathF.Max(overlay?.BoneLineWidth ?? 1.15f, 0.75f) * 0.5f);
        commandList.SetUniformVector3("CameraPosition", cameraPosition);
        commandList.SetUniformFloat("FadeStartDistance", 0.0f);
        commandList.SetUniformFloat("FadeEndDistance", 0.0f);
        commandList.Draw(_vertexCount, 0);
    }

    /// <inheritdoc/>
    protected override void ReleaseCore()
    {
        _pipeline?.Dispose();
        _pipeline = null;
        _lineBuffer?.Dispose();
        _lineBuffer = null;
        _lineBufferSizeBytes = 0;
        _vertexCount = -1;
    }

    private bool IsGeometryStale(Matrix4x4 localMatrix, bool hasParent, SkeletonOverlay? overlay)
    {
        return _vertexCount < 0
            || _lineBuffer is null
            || _lastLocalMatrix != localMatrix
            || _lastHasParent != hasParent
            || _lastAxisSize != (overlay?.BoneAxisSize ?? 0.2f)
            || _lastAxisScaleFromParent != (overlay?.BoneAxisScaleFromParent ?? 0.2f)
            || _lastAxisMaxSize != (overlay?.BoneAxisMaxSize ?? 2.0f)
            || _lastBoneAxisXColor != (overlay?.BoneAxisXColor ?? new Vector4(0.9f, 0.25f, 0.25f, 0.95f))
            || _lastBoneAxisYColor != (overlay?.BoneAxisYColor ?? new Vector4(0.25f, 0.9f, 0.25f, 0.95f))
            || _lastBoneAxisZColor != (overlay?.BoneAxisZColor ?? new Vector4(0.25f, 0.45f, 0.95f, 0.95f))
            || _lastBoneConnectionColor != (overlay?.BoneConnectionColor ?? new Vector4(0.88f, 0.88f, 0.76f, 0.9f))
            || _lastUseBoneNameHashColor != (overlay?.UseBoneNameHashColor ?? true)
            || _lastBoneNameColorSaturation != (overlay?.BoneNameColorSaturation ?? 0.62f)
            || _lastBoneNameColorValue != (overlay?.BoneNameColorValue ?? 0.95f);
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

    private static void BindLineVertexBuffer(ICommandList commandList, IGpuBuffer lineBuffer)
    {
        for (int slot = 0; slot < LineAttributeSlotCount; slot++)
        {
            commandList.BindBuffer(slot, lineBuffer);
        }
    }

    private static float[] BuildLineVertices(SkeletonBone bone, SkeletonOverlay? overlay)
    {
        float axisSize = MathF.Max(overlay?.BoneAxisSize ?? 0.2f, 0.0f);
        Matrix4x4 inverseLocal = Matrix4x4.Identity;
        bool hasParent = bone.Parent is not null && Matrix4x4.Invert(bone.GetActiveLocalMatrix(), out inverseLocal);
        Vector3 parentLocalOrigin = Vector3.Zero;
        if (hasParent)
        {
            parentLocalOrigin = Vector3.Transform(Vector3.Zero, inverseLocal);
            float parentDistance = parentLocalOrigin.Length();
            if (parentDistance > 1e-6f)
            {
                float scaledFromParent = parentDistance * MathF.Max(overlay?.BoneAxisScaleFromParent ?? 0.2f, 0.0f);
                axisSize = MathF.Max(axisSize, scaledFromParent);
            }
        }

        float axisMaxSize = overlay?.BoneAxisMaxSize ?? 2.0f;
        if (axisMaxSize > 0.0f)
        {
            axisSize = MathF.Min(axisSize, axisMaxSize);
        }

        int segmentCount = axisSize > 0.0f ? 3 : 0;
        if (hasParent)
        {
            segmentCount++;
        }

        float[] vertices = new float[segmentCount * 6 * FloatCountPerVertex];
        int offset = 0;
        if (axisSize > 0.0f)
        {
            AddLineSegment(vertices, ref offset, Vector3.Zero, Vector3.UnitX * axisSize, overlay?.BoneAxisXColor ?? new Vector4(0.9f, 0.25f, 0.25f, 0.95f), 1.0f);
            AddLineSegment(vertices, ref offset, Vector3.Zero, Vector3.UnitY * axisSize, overlay?.BoneAxisYColor ?? new Vector4(0.25f, 0.9f, 0.25f, 0.95f), 1.0f);
            AddLineSegment(vertices, ref offset, Vector3.Zero, Vector3.UnitZ * axisSize, overlay?.BoneAxisZColor ?? new Vector4(0.25f, 0.45f, 0.95f, 0.95f), 1.0f);
        }

        if (hasParent)
        {
            Vector4 connectionColor = overlay?.UseBoneNameHashColor ?? true
                ? BuildBoneNameColor(bone.Name, overlay)
                : overlay?.BoneConnectionColor ?? new Vector4(0.88f, 0.88f, 0.76f, 0.9f);
            AddLineSegment(vertices, ref offset, parentLocalOrigin, Vector3.Zero, connectionColor, 1.0f);
        }

        return vertices;
    }

    private static Vector4 BuildBoneNameColor(string boneName, SkeletonOverlay? overlay)
    {
        string source = string.IsNullOrWhiteSpace(boneName) ? "Bone" : boneName;
        uint hash = 2166136261u;
        for (int i = 0; i < source.Length; i++)
        {
            hash ^= source[i];
            hash *= 16777619u;
        }

        float hue = (hash % 360u) / 360.0f;
        float saturation = Math.Clamp(overlay?.BoneNameColorSaturation ?? 0.62f, 0.0f, 1.0f);
        float value = Math.Clamp(overlay?.BoneNameColorValue ?? 0.95f, 0.0f, 1.0f);
        Vector3 rgb = HsvToRgb(hue, saturation, value);
        float alpha = overlay?.BoneConnectionColor.W ?? 0.9f;
        return new Vector4(rgb.X, rgb.Y, rgb.Z, alpha);
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

    private void EnsurePipeline()
    {
        if (_pipeline is not null)
        {
            return;
        }

        MaterialTypeDefinition definition = _materialTypes.Get("Skeleton");
        _pipeline = definition.BuildPipeline(_graphicsDevice);
    }
}