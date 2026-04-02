using RedFox.Graphics2D;
using RedFox.Graphics2D.Conversion;

namespace RedFox.Tests.Graphics2D;

public sealed class TestConverterEngine : ConverterEngine
{
    private readonly bool _result;

    public TestConverterEngine(bool result)
    {
        _result = result;
    }

    public override string Name => "TestEngine";

    public int CallCount { get; private set; }

    public override bool TryConvert(
        ReadOnlySpan<byte> source,
        ImageFormat sourceFormat,
        Span<byte> destination,
        ImageFormat destinationFormat,
        int width,
        int height,
        ImageConvertFlags flags)
    {
        CallCount++;
        if (!_result)
            return false;

        destination.Clear();
        return true;
    }
}
