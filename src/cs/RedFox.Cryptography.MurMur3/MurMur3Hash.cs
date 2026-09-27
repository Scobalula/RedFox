// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using System.Security.Cryptography;
using System.Buffers.Binary;
using System.Numerics;

namespace RedFox.Cryptography.MurMur3;

/// <summary>
/// A class that computes the <see cref="MurMur3Hash"/> for the given input.
/// </summary>
public class MurMur3Hash : HashAlgorithm
{
    /// <summary>
    /// Gets the current hash value.
    /// </summary>
    public uint Value { get; private set; }

    /// <summary>
    /// Gets or Sets the seed.
    /// </summary>
    private uint Seed { get; set; }

    /// <summary>
    /// Gets or Sets the current length. This is required for <see cref="HashCore(byte[], int, int)"/>.
    /// </summary>
    private uint Length { get; set; }

    /// <summary>
    /// Gets or Sets the current tail value.
    /// </summary>
    private uint Tail { get; set; }

    private int TailLength { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MurMur3Hash"/> class with a seed of 0.
    /// </summary>
    public MurMur3Hash()
    {
        Seed = 0;
        Initialize();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MurMur3Hash"/> class with a given seed.
    /// </summary>
    /// <param name="seed">The seed to initialize the value at.</param>
    public MurMur3Hash(uint seed)
    {
        Seed = seed;
        Initialize();
    }

    /// <inheritdoc/>
    public override void Initialize()
    {
        Value = Seed;
        HashSizeValue = 32;
        Length = 0;
        Tail = 0;
        TailLength = 0;
    }

    /// <inheritdoc/>
    protected override void HashCore(byte[] array, int ibStart, int cbSize)
    {
        int position = ibStart;
        int end = checked(ibStart + cbSize);
        Length = unchecked(Length + (uint)cbSize);

        if (TailLength > 0)
        {
            while (TailLength < 4 && position < end)
            {
                Tail |= (uint)array[position++] << (TailLength * 8);
                TailLength++;
            }

            if (TailLength == 4)
            {
                MixBlock(Tail);
                Tail = 0;
                TailLength = 0;
            }
        }

        while (position + 4 <= end)
        {
            MixBlock(BinaryPrimitives.ReadUInt32LittleEndian(array.AsSpan(position, 4)));
            position += 4;
        }

        while (position < end)
        {
            Tail |= (uint)array[position++] << (TailLength * 8);
            TailLength++;
        }
    }

    /// <inheritdoc/>
    protected override byte[] HashFinal()
    {
        if (TailLength > 0)
        {
            uint tail = Tail * 0xcc9e2d51;
            tail = BitOperations.RotateLeft(tail, 15) * 0x1b873593;
            Value ^= tail;
        }

        Value = MurMur3.CalculateFinal32(Value, unchecked((int)Length));
        return BitConverter.GetBytes(Value);
    }

    private void MixBlock(uint block)
    {
        block *= 0xcc9e2d51;
        block = BitOperations.RotateLeft(block, 15);
        block *= 0x1b873593;
        Value ^= block;
        Value = BitOperations.RotateLeft(Value, 13) * 5 + 0xe6546b64;
    }
}
