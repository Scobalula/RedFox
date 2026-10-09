namespace RedFox.Compression.LZ4;

internal sealed class LZ4FrameContext : IDisposable
{
    private nint _context;
    private nint _dictionaryContext;
    private readonly bool _compress;
    private readonly byte[] _dictionary;
    private LZ4FramePreferences _preferences;
    private bool _started;
    private bool _complete;

    internal LZ4FrameContext(bool compress, LZ4FramePreferences preferences, byte[] dictionary)
    {
        _compress = compress;
        _preferences = preferences;
        _dictionary = dictionary;
        if (compress)
            LZ4Interop.Check(LZ4Interop.CreateCompressionContext(out _context, 100));
        else
            LZ4Interop.Check(LZ4Interop.CreateDecompressionContext(out _context, 100));
        if (compress && dictionary.Length != 0)
        {
            _dictionaryContext = LZ4Interop.CreateDictionary(dictionary, (nuint)dictionary.Length);
            if (_dictionaryContext == 0)
            {
                Dispose();
                throw new CompressionException("Failed to prepare the LZ4 frame dictionary.");
            }
        }
    }

    internal int Transform(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(_context == 0, this);
        if (_compress)
        {
            _preferences.FrameInfo.ContentSize = (ulong)source.Length;
            int compressedSize = checked((int)LZ4Interop.Check(LZ4Interop.FrameCompress(_context, destination, (nuint)destination.Length, source, (nuint)source.Length, _dictionaryContext, _preferences)));
            GC.KeepAlive(this);
            return compressedSize;
        }

        LZ4Interop.ResetDecompressionContext(_context);
        _complete = false;
        int read = 0;
        int written = 0;
        do
        {
            bool complete = Process(source[read..], destination[written..], 0, out int consumed, out int produced);
            read += consumed;
            written += produced;
            if (complete && read == source.Length)
                return written;
            if (consumed == 0 && produced == 0)
                throw new CompressionException("The LZ4 frame is truncated or the destination is too small.");
        } while (true);
    }

    internal bool Process(ReadOnlySpan<byte> source, Span<byte> destination, int operation, out int consumed, out int written)
    {
        consumed = 0;
        written = 0;
        if (_compress)
        {
            if (!_started)
            {
                written = checked((int)LZ4Interop.Check(LZ4Interop.Begin(_context, destination, (nuint)destination.Length, _dictionaryContext, _preferences)));
                _started = true;
                return false;
            }
            if (operation == 1)
            {
                written = checked((int)LZ4Interop.Check(LZ4Interop.Flush(_context, destination, (nuint)destination.Length, 0)));
            }
            else if (operation == 2)
            {
                written = checked((int)LZ4Interop.Check(LZ4Interop.End(_context, destination, (nuint)destination.Length, 0)));
            }
            else
            {
                written = checked((int)LZ4Interop.Check(LZ4Interop.Update(_context, destination, (nuint)destination.Length, source, (nuint)source.Length, 0)));
                consumed = source.Length;
            }

            GC.KeepAlive(this);
            return operation != 0;
        }

        if (_complete)
        {
            if (source.IsEmpty)
                return true;

            LZ4Interop.ResetDecompressionContext(_context);
        }

        nuint capacity = (nuint)destination.Length;
        nuint size = (nuint)source.Length;
        nuint result = LZ4Interop.Check(LZ4Interop.FrameDecompress(_context, destination, ref capacity, source, ref size, _dictionary, (nuint)_dictionary.Length, 0));
        GC.KeepAlive(this);
        consumed = (int)size;
        written = (int)capacity;
        _complete = result == 0;
        return _complete;
    }

    public void Dispose()
    {
        if (_context == 0)
            return;

        if (_compress)
            LZ4Interop.FreeCompressionContext(_context);
        else
            LZ4Interop.FreeDecompressionContext(_context);

        LZ4Interop.FreeDictionary(_dictionaryContext);
        _context = 0;
        _dictionaryContext = 0;
        GC.SuppressFinalize(this);
    }

    ~LZ4FrameContext() => Dispose();
}
