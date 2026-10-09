using System.Runtime.InteropServices;

namespace RedFox.Compression.Deflate;

internal sealed unsafe class ZLibContext : IDisposable
{
    private ZLibState* _state;
    private readonly bool _compress;
    private readonly int _windowBits;
    private readonly DeflateLevel _level;
    private readonly byte[] _dictionary;
    private bool _decompressionComplete;
    private bool _disposed;

    internal ZLibContext(bool compress, int windowBits, DeflateLevel level, byte[] dictionary)
    {
        _compress = compress;
        _windowBits = windowBits;
        _level = level;
        _dictionary = dictionary;
    }

    internal bool Process(ReadOnlySpan<byte> source, Span<byte> destination, int operation, out int consumed, out int written)
    {
        ZLibState* state = GetState();
        consumed = 0;
        written = 0;
        if (!_compress && _decompressionComplete)
        {
            if (source.IsEmpty)
                return true;

            Reset();
        }

        fixed (byte* input = source)
        fixed (byte* output = destination)
        {
            byte emptyOutput;
            state->NextIn = input;
            state->AvailableIn = (uint)source.Length;
            if (destination.IsEmpty)
                state->NextOut = &emptyOutput;
            else
                state->NextOut = output;
            state->AvailableOut = (uint)destination.Length;

            int flush = operation switch { 1 => 2, 2 => 4, _ => 0 };
            int status;
            if (_compress)
                status = DeflateInterop.Deflate(state, flush);
            else
                status = DeflateInterop.Inflate(state, 0);
            if (status == 2 && _dictionary.Length != 0)
            {
                fixed (byte* dictionary = _dictionary)
                    Check(state, DeflateInterop.InflateSetDictionary(state, dictionary, (uint)_dictionary.Length));

                status = DeflateInterop.Inflate(state, 0);
            }
            if (status == 2)
                throw new CompressionException("The zlib stream requires a dictionary.");

            Check(state, status);
            consumed = source.Length - (int)state->AvailableIn;
            written = destination.Length - (int)state->AvailableOut;
            if (!_compress)
                _decompressionComplete = status == 1;

            bool complete = status == 1 || (_compress && operation == 1 && state->AvailableOut != 0);
            GC.KeepAlive(this);
            return complete;
        }
    }

    internal int Transform(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        Reset();
        int read = 0;
        int written = 0;
        do
        {
            int operation = 0;
            if (_compress)
                operation = 2;

            bool complete = Process(source[read..], destination[written..], operation, out int consumed, out int produced);
            read += consumed;
            written += produced;
            if (complete && (_compress || read == source.Length))
                return written;
            if (consumed == 0 && produced == 0)
                throw new CompressionException("The input is truncated or the destination is too small.");
        } while (true);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_state == null)
        {
            _disposed = true;
            return;
        }

        int status;
        if (_compress)
            status = DeflateInterop.DeflateEnd(_state);
        else
            status = DeflateInterop.InflateEnd(_state);

        NativeMemory.Free(_state);
        _state = null;
        _disposed = true;

        if (disposing && status != 0)
            throw new CompressionException($"zlib cleanup failed ({status}).");
    }

    private ZLibState* GetState()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ZLibState* state = _state;
        if (state != null)
            return state;

        state = (ZLibState*)NativeMemory.AllocZeroed((nuint)sizeof(ZLibState));
        int status;
        if (_compress)
            status = DeflateInterop.DeflateInit(state, (int)_level, 8, _windowBits, 8, 0, DeflateInterop.Version(), sizeof(ZLibState));
        else
            status = DeflateInterop.InflateInit(state, _windowBits, DeflateInterop.Version(), sizeof(ZLibState));
        if (status != 0)
        {
            NativeMemory.Free(state);
            throw new CompressionException($"zlib initialization failed ({status}).");
        }

        _state = state;

        SetDictionary(state);
        return state;
    }

    private void SetDictionary(ZLibState* state)
    {
        if (_dictionary.Length == 0 || (!_compress && _windowBits > 0))
            return;

        fixed (byte* dictionary = _dictionary)
        {
            int status;
            if (_compress)
                status = DeflateInterop.DeflateSetDictionary(state, dictionary, (uint)_dictionary.Length);
            else
                status = DeflateInterop.InflateSetDictionary(state, dictionary, (uint)_dictionary.Length);

            Check(state, status);
        }
    }

    private static void Check(ZLibState* state, int status)
    {
        if (status < 0 && status != -5)
            throw new CompressionException($"zlib failed ({status}): {Marshal.PtrToStringUTF8(state->Message)}");
    }

    private void Reset()
    {
        ZLibState* state = GetState();
        int status;
        if (_compress)
            status = DeflateInterop.DeflateReset(state);
        else
            status = DeflateInterop.InflateReset(state);

        Check(state, status);
        if (!_compress)
            _decompressionComplete = false;

        SetDictionary(state);
    }

    ~ZLibContext() => Dispose(false);
}
