// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using RedFox;

namespace RedFox.Tests.Core;

public sealed class BytePatternAndPatternTests
{
    [Fact]
    public void Parse_WithWildcards_BuildsExpectedNeedleAndMask()
    {
        Pattern<byte> pattern = BytePattern.Parse("FF ?? 10 ?A AB");

        Assert.Equal([0xFF, 0x00, 0x10, 0x00, 0xAB], pattern.Needle);
        Assert.Equal([0x00, 0xFF, 0x00, 0xFF, 0x00], pattern.Mask);
    }

    [Fact]
    public void Parse_IgnoresWhitespaceAndInvalidPairs()
    {
        Pattern<byte> pattern = BytePattern.Parse(" 0A \n ZZ \t 1B ");

        Assert.Equal([0x0A, 0x1B], pattern.Needle);
        Assert.Equal([0x00, 0x00], pattern.Mask);
    }

    [Fact]
    public void Pattern_CanUpdateNeedleAndMask()
    {
        Pattern<byte> pattern = new([0x01], [0x00]);
        pattern.Needle = [0xAA, 0xBB];
        pattern.Mask = [0x00, 0xFF];

        Assert.Equal([0xAA, 0xBB], pattern.Needle);
        Assert.Equal([0x00, 0xFF], pattern.Mask);
    }
}
