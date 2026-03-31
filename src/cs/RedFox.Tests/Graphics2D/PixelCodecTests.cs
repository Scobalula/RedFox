using System.Reflection;
using RedFox.Graphics2D;
using RedFox.Graphics2D.BC;
using RedFox.Graphics2D.Codecs;

namespace RedFox.Tests.Graphics2D;

public sealed class PixelCodecTests
{
    [Fact]
    public void PixelCodec_SwitchCoversEveryConstructibleBuiltInCodecFormat()
    {
        HashSet<ImageFormat> expectedFormats = DiscoverSupportedFormats();

        Assert.NotEmpty(expectedFormats);

        foreach (ImageFormat format in Enum.GetValues<ImageFormat>())
        {
            bool expected = expectedFormats.Contains(format);
            bool actual = PixelCodec.TryGetCodec(format, out IPixelCodec? codec);

            Assert.Equal(expected, actual);

            if (actual)
            {
                Assert.NotNull(codec);
                Assert.Equal(format, codec.Format);
            }
        }
    }

    private static HashSet<ImageFormat> DiscoverSupportedFormats()
    {
        HashSet<ImageFormat> formats = [];
        Type codecInterfaceType = typeof(IPixelCodec);
        Assembly[] codecAssemblies = [typeof(R8G8B8A8Codec).Assembly, typeof(BC1Codec).Assembly];

        foreach (Assembly assembly in codecAssemblies.Distinct())
        {
            foreach (Type codecType in assembly.GetTypes())
            {
                if (codecType.IsAbstract || codecType.IsInterface || !codecInterfaceType.IsAssignableFrom(codecType))
                    continue;

                AddFormatsFromConstructors(formats, codecType);
            }
        }

        return formats;
    }

    private static void AddFormatsFromConstructors(HashSet<ImageFormat> formats, Type codecType)
    {
        ConstructorInfo? parameterlessConstructor = codecType.GetConstructor(Type.EmptyTypes);
        if (parameterlessConstructor is not null
            && parameterlessConstructor.Invoke(null) is IPixelCodec parameterlessCodec)
        {
            formats.Add(parameterlessCodec.Format);
        }

        ConstructorInfo? formatConstructor = codecType.GetConstructor([typeof(ImageFormat)]);
        if (formatConstructor is null)
            return;

        foreach (ImageFormat format in Enum.GetValues<ImageFormat>())
        {
            if (TryCreateCodec(formatConstructor, format, out IPixelCodec? codec) && codec is not null)
                formats.Add(codec.Format);
        }
    }

    private static bool TryCreateCodec(ConstructorInfo constructor, ImageFormat format, out IPixelCodec? codec)
    {
        try
        {
            codec = (IPixelCodec?)constructor.Invoke([format]);
            return codec is not null;
        }
        catch (TargetInvocationException)
        {
            codec = null;
            return false;
        }
        catch (ArgumentException)
        {
            codec = null;
            return false;
        }
    }
}
