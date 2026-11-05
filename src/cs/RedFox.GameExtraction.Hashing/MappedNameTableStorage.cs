using System.Buffers.Binary;
using System.IO.MemoryMappedFiles;
using System.Text;

namespace RedFox.GameExtraction.Hashing;

/// <summary>
/// Provides read-only mapped storage for packed name tables.
/// </summary>
public sealed unsafe class MappedNameTableStorage : IDisposable
{
    private readonly FileStream? _stream;
    private readonly bool _leaveOpen;
    private readonly MemoryMappedViewAccessor _view;
    private readonly long _keyOffset;
    private readonly long _nameOffset;
    private readonly long _bucketOffset;
    private readonly int _keySize;
    private readonly int _nameSize;
    private byte* _pointer;
    private bool _disposed;

    internal const ulong	Magic		= 0x004D454D414E4652;
    internal const int	Version		= 1;
    internal const int	HeaderSize	= 52;

    /// <summary>
    /// Gets the hash algorithm name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the entry count.
    /// </summary>
    public int Count { get; }

    /// <summary>
    /// Gets the lookup bucket count.
    /// </summary>
    public int BucketCount { get; }

    /// <summary>
    /// Gets the table metadata.
    /// </summary>
    public Dictionary<string, string> Metadata { get; } = [];

    /// <summary>
    /// Gets the packed key data while the storage remains alive and undisposed.
    /// </summary>
    public ReadOnlySpan<byte> Keys
    {
        get
        {
            ThrowIfDisposed();
            return new ReadOnlySpan<byte>(_pointer + _view.PointerOffset + _keyOffset, _keySize);
        }
    }

    /// <summary>
    /// Gets the packed UTF-8 name data while the storage remains alive and undisposed.
    /// </summary>
    public ReadOnlySpan<byte> Names
    {
        get
        {
            ThrowIfDisposed();
            return new ReadOnlySpan<byte>(_pointer + _view.PointerOffset + _nameOffset, _nameSize);
        }
    }

    /// <summary>
    /// Opens storage from a view containing exactly one complete mapped name table.
    /// </summary>
    /// <param name="view">The readable view, which must remain unchanged while the storage is in use.</param>
    /// <param name="leaveOpen">Whether to leave the view open when the storage is disposed.</param>
    public MappedNameTableStorage(MemoryMappedViewAccessor view, bool leaveOpen) : this(view, leaveOpen, null)
    {
    }

    /// <summary>
    /// Opens mapped storage over the entire supplied file without changing its position.
    /// </summary>
    /// <param name="stream">The readable file, which must remain unchanged while the storage is in use.</param>
    /// <param name="leaveOpen">Whether to leave the stream open when the storage is disposed.</param>
    public MappedNameTableStorage(FileStream stream, bool leaveOpen) : this(CreateView(stream), leaveOpen, stream)
    {
    }

    private MappedNameTableStorage(MemoryMappedViewAccessor view, bool leaveOpen, FileStream? stream)
    {
        ArgumentNullException.ThrowIfNull(view);

        _view		= view;
        _leaveOpen	= leaveOpen;
        _stream		= stream;

        try
        {
            if (!Environment.Is64BitProcess)
                throw new PlatformNotSupportedException("Mapped name tables require a 64-bit process.");

            if (!view.CanRead)
                throw new ArgumentException("The mapped view must be readable.", nameof(view));

            _view.SafeMemoryMappedViewHandle.AcquirePointer(ref _pointer);

            using var input = new UnmanagedMemoryStream(_pointer + _view.PointerOffset, _view.Capacity);
            using var reader = new BinaryReader(input, Encoding.UTF8, true);

            if (input.Length < HeaderSize || reader.ReadUInt64() != Magic || reader.ReadInt32() != Version)
                throw new InvalidDataException("The mapped name table header is invalid.");

            Count			= reader.ReadInt32();
            BucketCount		= reader.ReadInt32();
            _keySize		= reader.ReadInt32();
            _nameSize		= reader.ReadInt32();
            _keyOffset		= reader.ReadInt64();
            _nameOffset		= reader.ReadInt64();
            _bucketOffset	= reader.ReadInt64();

            if (Count < 0 || _keySize < 0 || _keySize > Array.MaxLength || _nameSize < 0 || _nameSize > Array.MaxLength || Count > _keySize / 5)
                throw new InvalidDataException("The mapped name table sizes are invalid.");

            if (BucketCount < 0 || (BucketCount & (BucketCount - 1)) != 0 || (Count == 0 && BucketCount != 0) || (Count > 0 && (BucketCount < 16 || Count > BucketCount - BucketCount / 4)))
                throw new InvalidDataException("The mapped name table bucket count is invalid.");

            if (_keyOffset < HeaderSize || _keyOffset > input.Length || _nameOffset < _keyOffset || _nameOffset > input.Length || _keySize > _nameOffset - _keyOffset)
                throw new InvalidDataException("The mapped name table key section is invalid.");

            if (_bucketOffset < _nameOffset || _bucketOffset > input.Length || _nameSize > _bucketOffset - _nameOffset)
                throw new InvalidDataException("The mapped name table name section is invalid.");

            if ((long)BucketCount * sizeof(int) != input.Length - _bucketOffset || (Count == 0 && (_keySize != 0 || _nameSize != 0)))
                throw new InvalidDataException("The mapped name table index section is invalid.");

            Name = ReadString(reader, _keyOffset);

            if (_keyOffset - input.Position < sizeof(int))
                throw new InvalidDataException("The mapped name table metadata is truncated.");

            var metadataCount = reader.ReadInt32();

            if (metadataCount < 0 || metadataCount > (_keyOffset - input.Position) / 2)
                throw new InvalidDataException("The mapped name table metadata count is invalid.");

            for (var i = 0; i < metadataCount; i++)
                Metadata[ReadString(reader, _keyOffset)] = ReadString(reader, _keyOffset);
        }
        catch
        {
            if (_pointer != null)
                _view.SafeMemoryMappedViewHandle.ReleasePointer();

            _pointer = null;

            if (stream is not null)
                _view.Dispose();

            throw;
        }
    }

    /// <summary>
    /// Releases the acquired view pointer if the storage was not disposed.
    /// </summary>
    ~MappedNameTableStorage()
    {
        if (_pointer != null)
            _view.SafeMemoryMappedViewHandle.ReleasePointer();
    }

    /// <summary>
    /// Opens and owns the mapped file at the supplied path.
    /// </summary>
    public static MappedNameTableStorage Open(string filePath)
    {
        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

        try
        {
            return new MappedNameTableStorage(stream, false);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Reads a lookup bucket containing a key-record offset plus one.
    /// </summary>
    public int ReadBucket(int bucket)
    {
        ThrowIfDisposed();

        if ((uint)bucket >= (uint)BucketCount)
            throw new InvalidDataException("The mapped name table bucket is invalid.");

        var offset = BinaryPrimitives.ReadInt32LittleEndian(new ReadOnlySpan<byte>(_pointer + _view.PointerOffset + _bucketOffset + (long)bucket * sizeof(int), sizeof(int)));

        if (offset < 0 || offset > _keySize)
            throw new InvalidDataException("The mapped name table entry offset is invalid.");

        GC.KeepAlive(this);
        return offset;
    }

    /// <summary>
    /// Releases the storage and closes its owned view and file.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _view.SafeMemoryMappedViewHandle.ReleasePointer();
        _pointer = null;

        if (_stream is not null || !_leaveOpen)
            _view.Dispose();

        if (!_leaveOpen)
            _stream?.Dispose();

        GC.SuppressFinalize(this);
    }

    internal void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    private static MemoryMappedViewAccessor CreateView(FileStream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (stream.Length < HeaderSize)
            throw new InvalidDataException("The mapped name table header is invalid.");

        using var mapping = MemoryMappedFile.CreateFromFile(stream, null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, true);
        return mapping.CreateViewAccessor(0, stream.Length, MemoryMappedFileAccess.Read);
    }

    private static string ReadString(BinaryReader reader, long end)
    {
        int length;

        try
        {
            length = reader.Read7BitEncodedInt();
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("The mapped name table string length is invalid.", exception);
        }

        if (length < 0 || length > end - reader.BaseStream.Position)
            throw new InvalidDataException("The mapped name table string is truncated.");

        var bytes = reader.ReadBytes(length);

        if (!System.Text.Unicode.Utf8.IsValid(bytes))
            throw new InvalidDataException("The mapped name table string is invalid.");

        return Encoding.UTF8.GetString(bytes);
    }
}
