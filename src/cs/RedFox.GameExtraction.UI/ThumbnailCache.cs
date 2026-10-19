using System.Buffers.Binary;
using System.IO.Hashing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using RedFox.Compression.LZ4;

namespace RedFox.GameExtraction.UI;

internal sealed class ThumbnailCache : IDisposable
{
    private const int FormatVersion		= 1;
    private const int HeaderSize		= 16;
    private const long MaxDataLength	= 512L * 1024 * 1024;

    private static readonly int RecordSize		= Unsafe.SizeOf<ThumbnailCacheRecord>();
    private static readonly ulong IndexMagic	= BinaryPrimitives.ReadUInt64LittleEndian("RFTHUMBI"u8);
    private static readonly ulong DataMagic		= BinaryPrimitives.ReadUInt64LittleEndian("RFTHUMBD"u8);

    private readonly Dictionary<UInt128, ThumbnailCacheRecord> _records = [];

    private readonly Lock           _sync			= new();
    private readonly LZ4Codec		_codec			= new();
    private readonly Mutex			_mutex;
    private readonly SafeFileHandle	_index;
    private readonly SafeFileHandle	_data;
    private long					_indexLength	= HeaderSize;
    private bool					_disposed;

    public ThumbnailCache(string directory)
    {
        string fullDirectory	= Path.GetFullPath(directory);
        string indexPath		= Path.Combine(fullDirectory, "thumbnails.rfti");
        string dataPath			= Path.Combine(fullDirectory, "thumbnails.rftd");

        Directory.CreateDirectory(fullDirectory);
        _mutex = new Mutex(false, $@"Local\RedFox.Thumbnails.{XxHash64.HashToUInt64(Encoding.UTF8.GetBytes(fullDirectory.ToUpperInvariant())):X16}");
        AcquireMutex();

        try
        {
            CompactIfAlone(indexPath, dataPath);
            _index	= OpenShared(indexPath);
            _data	= OpenShared(dataPath);

            if (!HasHeader(_index, IndexMagic) || !HasHeader(_data, DataMagic))
            {
                ResetFile(_data, DataMagic);
                ResetFile(_index, IndexMagic);
            }

            LoadNewRecords();
        }
        finally
        {
            _mutex.ReleaseMutex();
        }
    }

    public static UInt128 CreateKey(string text) => XxHash128.HashToUInt128(Encoding.UTF8.GetBytes(text));

    public bool TryGet(UInt128 key, out ThumbnailCacheImage image)
    {
        image = default;

        lock (_sync)
        {
            if (_disposed)
            {
                return false;
            }

            if (!_records.TryGetValue(key, out ThumbnailCacheRecord record))
            {
                LoadNewRecords();

                if (!_records.TryGetValue(key, out record))
                {
                    return false;
                }
            }

            byte[] compressed	= new byte[record.CompressedLength];
            byte[] pixels		= new byte[record.Width * record.Height * 4 * record.FrameCount];

            if (RandomAccess.Read(_data, compressed, record.Offset) != compressed.Length || XxHash32.HashToUInt32(compressed) != record.Checksum || _codec.Decompress(compressed, pixels) != pixels.Length)
            {
                _records.Remove(key);
                return false;
            }

            image = new ThumbnailCacheImage(record.Width, record.Height, record.FrameCount, pixels);
            return true;
        }
    }

    public void Store(UInt128 key, ThumbnailCacheImage image)
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            byte[] compressed = _codec.Compress(image.Pixels);
            AcquireMutex();

            try
            {
                long offset = RandomAccess.GetLength(_data);
                RandomAccess.Write(_data, compressed, offset);

                ThumbnailCacheRecord record	= new(key, offset, compressed.Length, (ushort)image.Width, (ushort)image.Height, image.FrameCount, XxHash32.HashToUInt32(compressed));
                long recordOffset			= HeaderSize + (RandomAccess.GetLength(_index) - HeaderSize) / RecordSize * RecordSize;

                RandomAccess.Write(_index, MemoryMarshal.AsBytes(new ReadOnlySpan<ThumbnailCacheRecord>(in record)), recordOffset);
                _records[key] = record;
            }
            finally
            {
                _mutex.ReleaseMutex();
            }
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _disposed = true;
            _index.Dispose();
            _data.Dispose();
            _codec.Dispose();
            _mutex.Dispose();
        }
    }

    private static SafeFileHandle OpenShared(string path) => File.OpenHandle(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);

    private static bool HasHeader(SafeFileHandle handle, ulong magic)
    {
        Span<byte> header = stackalloc byte[HeaderSize];
        return RandomAccess.Read(handle, header, 0) == HeaderSize && BinaryPrimitives.ReadUInt64LittleEndian(header) == magic && BinaryPrimitives.ReadInt32LittleEndian(header[8..]) == FormatVersion;
    }

    private static void ResetFile(SafeFileHandle handle, ulong magic)
    {
        Span<byte> header = stackalloc byte[HeaderSize];
        BinaryPrimitives.WriteUInt64LittleEndian(header, magic);
        BinaryPrimitives.WriteInt32LittleEndian(header[8..], FormatVersion);

        RandomAccess.SetLength(handle, 0);
        RandomAccess.Write(handle, header, 0);
    }

    private static ThumbnailCacheRecord[] ReadRecords(SafeFileHandle index, long offset)
    {
        ThumbnailCacheRecord[] records	= new ThumbnailCacheRecord[Math.Max(0, (RandomAccess.GetLength(index) - offset) / RecordSize)];
        int read						= RandomAccess.Read(index, MemoryMarshal.AsBytes(records.AsSpan()), offset);

        return read == records.Length * RecordSize ? records : records[..(read / RecordSize)];
    }

    private static void CompactIfAlone(string indexPath, string dataPath)
    {
        if (!File.Exists(indexPath) || !File.Exists(dataPath) || new FileInfo(dataPath).Length <= MaxDataLength)
        {
            return;
        }

        string compactIndexPath	= indexPath + ".tmp";
        string compactDataPath	= dataPath + ".tmp";

        try
        {
            using SafeFileHandle index	= File.OpenHandle(indexPath, FileMode.Open, FileAccess.Read, FileShare.None);
            using SafeFileHandle data	= File.OpenHandle(dataPath, FileMode.Open, FileAccess.Read, FileShare.None);

            if (!HasHeader(index, IndexMagic) || !HasHeader(data, DataMagic))
            {
                return;
            }

            ThumbnailCacheRecord[] records	= ReadRecords(index, HeaderSize);
            List<ThumbnailCacheRecord> kept	= [];
            HashSet<UInt128> keys			= [];
            long keptLength					= 0;

            for (int i = records.Length - 1; i >= 0 && keptLength < MaxDataLength / 2; i--)
            {
                if (keys.Add(records[i].Key))
                {
                    kept.Add(records[i]);
                    keptLength += records[i].CompressedLength;
                }
            }

            kept.Reverse();

            using SafeFileHandle compactIndex	= File.OpenHandle(compactIndexPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            using SafeFileHandle compactData	= File.OpenHandle(compactDataPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            ThumbnailCacheRecord[] compacted	= new ThumbnailCacheRecord[kept.Count];
            long offset							= HeaderSize;

            ResetFile(compactIndex, IndexMagic);
            ResetFile(compactData, DataMagic);

            for (int i = 0; i < kept.Count; i++)
            {
                byte[] block = new byte[kept[i].CompressedLength];
                RandomAccess.Read(data, block, kept[i].Offset);
                RandomAccess.Write(compactData, block, offset);

                compacted[i]	= kept[i] with { Offset = offset };
                offset			+= block.Length;
            }

            RandomAccess.Write(compactIndex, MemoryMarshal.AsBytes(compacted.AsSpan()), HeaderSize);
        }
        catch (IOException)
        {
            return;
        }

        File.Move(compactDataPath, dataPath, true);
        File.Move(compactIndexPath, indexPath, true);
    }

    private void AcquireMutex()
    {
        try
        {
            _mutex.WaitOne();
        }
        catch (AbandonedMutexException)
        {
        }
    }

    private void LoadNewRecords()
    {
        long dataLength					= RandomAccess.GetLength(_data);
        ThumbnailCacheRecord[] records	= ReadRecords(_index, _indexLength);
        _indexLength					+= (long)records.Length * RecordSize;

        foreach (ThumbnailCacheRecord record in records)
        {
            if (record.Offset >= HeaderSize && record.CompressedLength > 0 && record.FrameCount > 0 && record.Offset + record.CompressedLength <= dataLength)
            {
                _records[record.Key] = record;
            }
        }
    }
}
