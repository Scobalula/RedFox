using RedFox.Graphics2D.Codecs;
using RedFox.Graphics2D.Conversion;

namespace RedFox.Graphics2D.BlockCompression.Vulkan;

/// <summary>
/// Vulkan-backed converter engine for BC decode and BC6H/BC7 encode workflows.
/// </summary>
public sealed class VulkanBcConverterEngine : ConverterEngine, IDisposable
{
    private readonly VulkanBcConverterEngineOptions _options;
    private readonly VulkanBcContext? _context;
    private bool _disposed;

    /// <summary>
    /// Initializes a new Vulkan BC converter engine with default options.
    /// </summary>
    public VulkanBcConverterEngine() : this(new VulkanBcConverterEngineOptions())
    {
    }

    /// <summary>
    /// Initializes a new Vulkan BC converter engine with custom options.
    /// </summary>
    /// <param name="options">The engine options.</param>
    public VulkanBcConverterEngine(VulkanBcConverterEngineOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _context = VulkanBcContext.Create();
        IsAvailable = _context is not null;
    }

    /// <inheritdoc />
    public override string Name => "VulkanBC";

    /// <summary>
    /// Gets whether a Vulkan compute device and the embedded BC shaders are available.
    /// </summary>
    public bool IsAvailable { get; }

    /// <inheritdoc />
    public override bool TryConvert(ReadOnlySpan<byte> source, ImageFormat sourceFormat, Span<byte> destination, ImageFormat destinationFormat, int width, int height, ImageConvertFlags flags)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_context is not null && _context.TryConvert(source, sourceFormat, destination, destinationFormat, width, height, flags))
            return true;
        if (!_options.AllowCpuBridgeConversion)
            return false;
        if (!VulkanBcFormatMap.IsBcFormat(sourceFormat) && !VulkanBcFormatMap.IsBcFormat(destinationFormat))
            return false;

        IPixelCodec sourceCodec = PixelCodec.GetCodec(sourceFormat);
        IPixelCodec destinationCodec = PixelCodec.GetCodec(destinationFormat);
        destinationCodec.ConvertFrom(source, sourceCodec, destination, width, height);
        return true;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _context?.Dispose();
        _disposed = true;
    }
}
