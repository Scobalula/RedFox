using RedFox.Imaging;
using RedFox.Imaging.Primitives;
using RedFox.Imaging.Processing;

namespace RedFox.Tests.Imaging;

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
