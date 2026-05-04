using System;
using RedFox.Graphics3D.Buffers;

namespace RedFox.Graphics3D.Rendering;

/// <summary>
/// Describes one mesh-owned GPU buffer binding and tracks the uploaded GPU resource for that binding.
/// </summary>
public sealed class MeshGpuBufferBinding
{
    private readonly Func<MeshGpuBufferBinding, IGraphicsDevice, bool>? _customUpdater;
    private const int ElementStrideKind = 0;
    private const int ValueStrideKind = 1;
    private const int ComponentStrideKind = 2;

    private readonly bool _normalizeIndexElementType;
    private readonly int _strideKind;

    private GpuBufferElementType _elementType = GpuBufferElementType.Unknown;
    private int _sizeBytes;
    private int _strideBytes;

    private MeshGpuBufferBinding(
        DataBuffer? data,
        string shaderName,
        BufferUsage usage,
        int strideKind,
        bool normalizeIndexElementType,
        Func<MeshGpuBufferBinding, IGraphicsDevice, bool>? customUpdater)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shaderName);

        Data = data;
        ShaderName = shaderName;
        Usage = usage;
        _strideKind = strideKind;
        _normalizeIndexElementType = normalizeIndexElementType;
        _customUpdater = customUpdater;
    }

    /// <summary>
    /// Gets the shader-visible name used to resolve the backend binding slot.
    /// </summary>
    public string ShaderName { get; }

    /// <summary>
    /// Gets the mesh data source associated with this binding.
    /// </summary>
    public DataBuffer? Data { get; }

    /// <summary>
    /// Gets the GPU usage flags used when the backing buffer is created.
    /// </summary>
    public BufferUsage Usage { get; }

    /// <summary>
    /// Gets the current GPU buffer instance, when one has been created.
    /// </summary>
    public IGpuBuffer? GpuBuffer { get; private set; }

    /// <summary>
    /// Gets a value indicating whether this binding currently owns a live GPU buffer.
    /// </summary>
    public bool HasGpuBuffer => GpuBuffer is { IsDisposed: false };

    /// <summary>
    /// Gets the total number of source components represented by the uploaded data.
    /// </summary>
    public int TotalComponentCount { get; private set; }

    /// <summary>
    /// Creates a mesh binding for a vertex input stream.
    /// </summary>
    /// <param name="data">The mesh data source to upload.</param>
    /// <param name="shaderName">The shader-visible vertex attribute name.</param>
    /// <returns>A mesh buffer binding configured for vertex input.</returns>
    public static MeshGpuBufferBinding CreateVertex(DataBuffer? data, string shaderName)
        => new(data, shaderName, BufferUsage.Vertex, ElementStrideKind, normalizeIndexElementType: false, customUpdater: null);

    /// <summary>
    /// Creates a mesh binding for an index buffer.
    /// </summary>
    /// <param name="data">The mesh data source to upload.</param>
    /// <param name="name">The binding name used for diagnostics.</param>
    /// <returns>A mesh buffer binding configured for index input.</returns>
    public static MeshGpuBufferBinding CreateIndex(DataBuffer? data, string name)
        => new(data, name, BufferUsage.Index, ComponentStrideKind, normalizeIndexElementType: true, customUpdater: null);

    /// <summary>
    /// Creates a mesh binding for a shader resource buffer.
    /// </summary>
    /// <param name="data">The mesh data source to upload.</param>
    /// <param name="shaderName">The shader-visible resource name.</param>
    /// <param name="usage">The GPU usage flags for the resource buffer.</param>
    /// <param name="normalizeIndexElementType">True to normalize signed integer index element types to unsigned GPU element types.</param>
    /// <returns>A mesh buffer binding configured for shader-resource binding.</returns>
    public static MeshGpuBufferBinding CreateShaderResource(
        DataBuffer? data,
        string shaderName,
        BufferUsage usage,
        bool normalizeIndexElementType)
    {
        if (usage.HasFlag(BufferUsage.Index) || usage.HasFlag(BufferUsage.Vertex))
        {
            throw new ArgumentException("Shader-resource bindings cannot be created with vertex or index buffer usage flags.", nameof(usage));
        }

        return new(data, shaderName, usage, ValueStrideKind, normalizeIndexElementType, customUpdater: null);
    }

    /// <summary>
    /// Creates a mesh binding for a shader resource buffer backed by a custom updater.
    /// </summary>
    /// <param name="shaderName">The shader-visible resource name.</param>
    /// <param name="usage">The GPU usage flags for the resource buffer.</param>
    /// <param name="normalizeIndexElementType">True to normalize signed integer index element types to unsigned GPU element types.</param>
    /// <param name="customUpdater">The callback that fully owns how the binding updates its GPU buffer.</param>
    /// <returns>A mesh buffer binding configured for shader-resource binding.</returns>
    public static MeshGpuBufferBinding CreateShaderResource(
        string shaderName,
        BufferUsage usage,
        bool normalizeIndexElementType,
        Func<MeshGpuBufferBinding, IGraphicsDevice, bool> customUpdater)
    {
        ArgumentNullException.ThrowIfNull(customUpdater);

        if (usage.HasFlag(BufferUsage.Index) || usage.HasFlag(BufferUsage.Vertex))
        {
            throw new ArgumentException("Shader-resource bindings cannot be created with vertex or index buffer usage flags.", nameof(usage));
        }

        return new(data: null, shaderName, usage, ValueStrideKind, normalizeIndexElementType, customUpdater);
    }

    /// <summary>
    /// Updates the GPU buffer from the associated mesh data source or custom updater.
    /// </summary>
    /// <param name="graphicsDevice">The graphics device used to create or update the GPU resource.</param>
    /// <returns><see langword="true"/> when the binding has a valid GPU buffer after the update; otherwise <see langword="false"/>.</returns>
    public bool Update(IGraphicsDevice graphicsDevice)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);

        if (_customUpdater is not null)
        {
            return _customUpdater(this, graphicsDevice);
        }

        if (HasGpuBuffer)
        {
            return true;
        }

        return UpdateFromSource(graphicsDevice, Data);
    }

    private bool UpdateFromSource(IGraphicsDevice graphicsDevice, DataBuffer? sourceData)
    {
        if (sourceData is not { ElementCount: > 0 })
        {
            Release();
            return false;
        }

        GpuBufferData data = GetRequiredGpuBufferData(sourceData, ShaderName);
        if (_normalizeIndexElementType)
        {
            data = NormalizeIndexElementType(data);
        }

        return UpdateCore(graphicsDevice, data);
    }

    /// <summary>
    /// Updates the GPU buffer from generated data that is not owned by a persistent <see cref="DataBuffer"/> instance.
    /// </summary>
    /// <param name="graphicsDevice">The graphics device used to create or update the GPU resource.</param>
    /// <param name="data">The generated buffer payload to upload.</param>
    /// <returns><see langword="true"/> when the binding has a valid GPU buffer after the update; otherwise <see langword="false"/>.</returns>
    public bool UpdateGenerated(IGraphicsDevice graphicsDevice, GpuBufferData data)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        return UpdateCore(graphicsDevice, data);
    }

    /// <summary>
    /// Binds the current GPU buffer to a command list by resolving the target slot from the active pipeline.
    /// </summary>
    /// <param name="commandList">The command list receiving the binding.</param>
    /// <param name="pipeline">The active pipeline used to resolve the binding slot.</param>
    public void Bind(ICommandList commandList, IGpuPipelineState pipeline)
    {
        ArgumentNullException.ThrowIfNull(commandList);
        ArgumentNullException.ThrowIfNull(pipeline);

        if (GpuBuffer is not { IsDisposed: false } buffer)
        {
            return;
        }

        if (Usage.HasFlag(BufferUsage.Index))
        {
            commandList.BindIndexBuffer(buffer);
            return;
        }

        if (!pipeline.TryGetBufferSlot(ShaderName, out int slot))
        {
            return;
        }

        commandList.BindBuffer(slot, buffer);
        if (!Usage.HasFlag(BufferUsage.Vertex))
        {
            commandList.SetUniformInt(ShaderName, slot);
        }
    }

    /// <summary>
    /// Releases the current GPU buffer while keeping the binding descriptor available for later reuse.
    /// </summary>
    public void Release()
    {
        GpuBuffer?.Dispose();
        GpuBuffer = null;
        _sizeBytes = 0;
        _strideBytes = 0;
        _elementType = GpuBufferElementType.Unknown;
        TotalComponentCount = 0;
    }

    private static GpuBufferData GetRequiredGpuBufferData(DataBuffer buffer, string bufferName)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        if (buffer.TryGetGpuBufferData(out GpuBufferData data))
        {
            return data;
        }

        throw new NotSupportedException($"Buffer '{bufferName}' does not expose a direct GPU-compatible payload.");
    }

    private static GpuBufferData NormalizeIndexElementType(GpuBufferData data)
    {
        GpuBufferElementType elementType = data.ElementType switch
        {
            GpuBufferElementType.Int8 => GpuBufferElementType.UInt8,
            GpuBufferElementType.Int16 => GpuBufferElementType.UInt16,
            GpuBufferElementType.Int32 => GpuBufferElementType.UInt32,
            _ => data.ElementType,
        };

        return elementType == data.ElementType
            ? data
            : new GpuBufferData(
                data.Bytes,
                elementType,
                data.ElementCount,
                data.ValueCount,
                data.ComponentCount,
                data.ElementStrideBytes,
                data.ValueStrideBytes,
                data.ComponentSizeBytes);
    }

    private int GetStride(GpuBufferData data)
    {
        return _strideKind switch
        {
            ElementStrideKind => data.ElementStrideBytes,
            ValueStrideKind => data.ValueStrideBytes,
            ComponentStrideKind => data.ComponentSizeBytes,
            _ => data.ElementStrideBytes,
        };
    }

    private bool UpdateCore(IGraphicsDevice graphicsDevice, GpuBufferData data)
    {
        int stride = GetStride(data);
        if (data.Bytes.IsEmpty || stride <= 0)
        {
            Release();
            return false;
        }

        if (GpuBuffer is null
            || GpuBuffer.IsDisposed
            || _sizeBytes != data.SizeBytes
            || _strideBytes != stride
            || _elementType != data.ElementType
            || GpuBuffer.SizeBytes != data.SizeBytes
            || GpuBuffer.StrideBytes != stride
            || GpuBuffer.Usage != Usage
            || GpuBuffer.ElementType != data.ElementType)
        {
            GpuBuffer?.Dispose();
            GpuBuffer = graphicsDevice.CreateBuffer(data.SizeBytes, stride, Usage, data.ElementType, data.Bytes);
        }
        else
        {
            graphicsDevice.UpdateBuffer(GpuBuffer, data.Bytes);
        }

        _sizeBytes = data.SizeBytes;
        _strideBytes = stride;
        _elementType = data.ElementType;
        TotalComponentCount = data.TotalComponentCount;
        return true;
    }
}