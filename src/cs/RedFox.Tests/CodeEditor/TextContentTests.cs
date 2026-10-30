using System.Text;
using RedFox.Avalonia.CodeEditor;

namespace RedFox.Tests.CodeEditor;

public sealed class TextContentTests
{
    [Fact]
    public void Utf8IsText()
    {
        Assert.True(TextContent.TryDecode(Encoding.UTF8.GetBytes("bbenable mpxp é"), out string? text));
        Assert.Equal("bbenable mpxp é", text);
    }

    [Fact]
    public void BytesThatAreNotUtf8FallBackToLatin1()
    {
        Assert.True(TextContent.TryDecode([0x63, 0x61, 0x66, 0xE9], out string? text));
        Assert.Equal("café", text);
    }

    [Fact]
    public void EmptyDataIsText()
    {
        Assert.True(TextContent.TryDecode([], out string? text));
        Assert.Empty(text);
    }

    [Fact]
    public void AByteOrderMarkIsRemoved()
    {
        Assert.True(TextContent.TryDecode([0xEF, 0xBB, 0xBF, 0x61], out string? text));
        Assert.Equal("a", text);
    }

    [Theory]
    [InlineData(new byte[] { 0xFF, 0xFE, 0x61, 0x00, 0x62, 0x00 })]
    [InlineData(new byte[] { 0xFE, 0xFF, 0x00, 0x61, 0x00, 0x62 })]
    [InlineData(new byte[] { 0x61, 0x00, 0x62, 0x00 })]
    [InlineData(new byte[] { 0x00, 0x61, 0x00, 0x62 })]
    public void Utf16IsText(byte[] data)
    {
        Assert.True(TextContent.TryDecode(data, out string? text));
        Assert.Equal("ab", text);
    }

    [Fact]
    public void ATrailingNulIsRemoved()
    {
        byte[] data = [.. Enumerable.Repeat((byte)0x61, 600), 0];

        Assert.True(TextContent.TryDecode(data, out string? text));
        Assert.Equal(600, text.Length);
    }

    [Theory]
    [InlineData(new byte[] { 0x1B, 0x4C, 0x75, 0x61, 0x51, 0x00, 0x01, 0x04 })]
    [InlineData(new byte[] { 0x00, 0x00, 0x00, 0x00 })]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x00, 0x00, 0x00, 0x0D })]
    public void NulBytesInTheSampleAreBinary(byte[] data)
    {
        Assert.False(TextContent.TryDecode(data, out _));
    }
}
