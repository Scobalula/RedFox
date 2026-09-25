using RedFox.Imaging.Primitives;

namespace RedFox.Imaging.Formats.Ktx;

internal readonly record struct KtxFormatDescriptor(ImageFormat ImageFormat, uint GlType, uint GlTypeSize, uint GlFormat, uint GlInternalFormat, uint GlBaseInternalFormat, bool IsCompressed);
