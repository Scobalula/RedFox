// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace RedFox.Cryptography.MurMur3
{
    /// <summary>
    /// A class that provides methods for computing MurMur3 hashes.
    /// </summary>
    public static class MurMur3
    {
        /// <summary>
        /// Computes a 32bit MurMur3 hash for the given input.
        /// </summary>
        /// <param name="buffer">The buffer to compute the hash for.</param>
        /// <param name="seed">The seed to use as initializer.</param>
        /// <returns>Resulting computed value.</returns>
        public static uint Calculate32UTF8(string buffer, uint seed) => Calculate32(buffer, seed, Encoding.UTF8);

        /// <summary>
        /// Computes a 32bit MurMur3 hash for the given input.
        /// </summary>
        /// <param name="buffer">The buffer to compute the hash for.</param>
        /// <param name="seed">The seed to use as initializer.</param>
        /// <returns>Resulting computed value.</returns>
        public static uint Calculate32UTF16(string buffer, uint seed) => Calculate32(buffer, seed, Encoding.Unicode);

        /// <summary>
        /// Computes a 32bit MurMur3 hash for the given input.
        /// </summary>
        /// <param name="buffer">The buffer to compute the hash for.</param>
        /// <param name="seed">The seed to use as initializer.</param>
        /// <param name="encoding">The encoding to use to decode the string.</param>
        /// <returns>Resulting computed value.</returns>
        public static uint Calculate32(string buffer, uint seed, Encoding encoding) => Calculate32(encoding.GetBytes(buffer), seed);

        /// <summary>
        /// Computes a 32bit MurMur3 hash for the given input.
        /// </summary>
        /// <param name="buffer">The buffer to compute the hash for.</param>
        /// <param name="seed">The seed to use as initializer.</param>
        /// <returns>Resulting computed value.</returns>
        public static uint Calculate32(byte[] buffer, uint seed)
        {
            var result = CalculateBlock32(buffer, seed);
            return CalculateFinal32(result, buffer.Length);
        }

        /// <summary>
        /// Computes a hash for the block with the given seed.
        /// </summary>
        /// <param name="buffer">The buffer to compute the hash for.</param>
        /// <param name="seed">The seed to use as initializer.</param>
        /// <returns>Resulting computed value.</returns>
        public static uint CalculateBlock32(byte[] buffer, uint seed)
        {
            var b = MemoryMarshal.Cast<byte, uint>(buffer);

            uint h1 = seed;
            uint c1 = 0xcc9e2d51;
            uint c2 = 0x1b873593;

            for (int i = 0; i < b.Length; i++)
            {
                uint k1 = b[i];

                k1 *= c1;
                k1 = BitOperations.RotateLeft(k1, 15);
                k1 *= c2;

                h1 ^= k1;
                h1 = BitOperations.RotateLeft(h1, 13);
                h1 = h1 * 5 + 0xe6546b64;
            }

            uint final = 0;
            var tail = buffer.Length & 3;
            var tailIndex = b.Length * 4;

            if (tail >= 3)
            {
                final ^= (uint)(buffer[tailIndex + 2] << 16);
            }
            if (tail >= 2)
            {
                final ^= (uint)(buffer[tailIndex + 1] << 8);
            }
            if (tail >= 1)
            {
                final ^= buffer[tailIndex + 0];
                final *= c1;
                final = BitOperations.RotateLeft(final, 15);
                final *= c2;
                h1 ^= final;
            }

            return h1;
        }

        /// <summary>
        /// Computes the final value for the given input.
        /// </summary>
        /// <param name="result">The resulting value.</param>
        /// <param name="length">The length of the data.</param>
        /// <returns>The resulting value.</returns>
        public static uint CalculateFinal32(uint result, int length)
        {
            result ^= (uint)length;

            result ^= result >> 16;
            result *= 0x85ebca6b;
            result ^= result >> 13;
            result *= 0xc2b2ae35;
            result ^= result >> 16;

            return result;
        }
    }
}
