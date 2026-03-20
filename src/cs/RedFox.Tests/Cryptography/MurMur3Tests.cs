// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using System.Security.Cryptography;
using System.Text;
using RedFox.Cryptography.MurMur3;

namespace RedFox.Tests.Cryptography;

public sealed class MurMur3Tests
{
    [Fact]
    public void Calculate32_WithUtf8AndExplicitEncoding_Match()
    {
        const string input = "RedFox";
        const uint seed = 0x11223344;

        uint viaUtf8 = MurMur3.Calculate32UTF8(input, seed);
        uint viaEncoding = MurMur3.Calculate32(input, seed, Encoding.UTF8);

        Assert.Equal(viaEncoding, viaUtf8);
    }

    [Fact]
    public void Calculate32_EqualsBlockPlusFinal()
    {
        byte[] data = Encoding.ASCII.GetBytes("abcdefghi");
        const uint seed = 1234;

        uint block = MurMur3.CalculateBlock32(data, seed);
        uint expected = MurMur3.CalculateFinal32(block, data.Length);
        uint actual = MurMur3.Calculate32(data, seed);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Calculate32Utf8_AndUtf16_ProduceDifferentValuesForAscii()
    {
        const string input = "same-text";

        uint utf8 = MurMur3.Calculate32UTF8(input, 0);
        uint utf16 = MurMur3.Calculate32UTF16(input, 0);

        Assert.NotEqual(utf8, utf16);
    }

    [Fact]
    public void MurMur3Hash_ComputeHash_MatchesCalculate32()
    {
        byte[] data = Encoding.UTF8.GetBytes("hash-me");
        const uint seed = 0xAABBCCDD;
        uint expected = MurMur3.Calculate32(data, seed);

        using MurMur3Hash hash = new(seed);
        byte[] hashBytes = hash.ComputeHash(data);
        uint actual = BitConverter.ToUInt32(hashBytes, 0);

        Assert.Equal(expected, actual);
        Assert.Equal(seed, hash.Value);
    }

    [Fact]
    public void MurMur3Hash_Initialize_ResetsToSeed()
    {
        using MurMur3Hash hash = new(42);
        _ = hash.ComputeHash(Encoding.UTF8.GetBytes("abc"));
        Assert.Equal((uint)42, hash.Value);

        hash.Initialize();
        Assert.Equal((uint)42, hash.Value);
    }
}
