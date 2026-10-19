using System.Runtime.InteropServices;

namespace RedFox.GameExtraction.UI;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly record struct ThumbnailCacheRecord(UInt128 Key, long Offset, int CompressedLength, ushort Width, ushort Height, int FrameCount, uint Checksum);
