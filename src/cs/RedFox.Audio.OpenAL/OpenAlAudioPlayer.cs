// --------------------------------------------------------------------------------------
// RedFox Utility Library - MIT License
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

using System.Runtime.InteropServices;
using Silk.NET.OpenAL;

namespace RedFox.Audio.OpenAL;

/// <summary>
/// Streams <see cref="AudioClip"/> instances through an OpenAL Soft device and context.
/// The clip is decoded a few hundred milliseconds ahead of playback on a background thread, so memory use does not grow with its length.
/// Clips with more than two channels are downmixed to their first two channels.
/// </summary>
public sealed unsafe class OpenAlAudioPlayer : IAudioPlayer
{
    private const int BufferCount = 4;
    private const int BufferMilliseconds = 250;
    private const int PumpIntervalMilliseconds = 20;

    private readonly OpenAlDevice _device;
    private readonly AL _al;
    private readonly uint _source;
    private readonly Stack<uint> _freeBuffers = new();
    private readonly Queue<QueuedBuffer> _queuedBuffers = new();
    private readonly Lock _lock = new();
    private readonly Thread _pump;
    private AudioClip? _clip;
    private AudioDecoder? _decoder;
    private BufferFormat _bufferFormat;
    private byte[] _decoded = [];
    private short[] _converted = [];
    private short[] _output = [];
    private int _bufferFrames;
    private long _nextFrame;
    private long? _pendingSeek;
    private bool _endOfStream;
    private AudioPlaybackState _state;
    private volatile bool _disposed;

    /// <summary>
    /// Creates a streaming source on the default OpenAL output device, which is shared by every player in the process, and starts the thread that keeps it fed.
    /// </summary>
    /// <exception cref="AudioException">Thrown when no output device can be opened.</exception>
    public OpenAlAudioPlayer()
    {
        _device = OpenAlDevice.Acquire();
        _al = _device.Al;
        _source = _al.GenSource();

        for (int i = 0; i < BufferCount; i++)
            _freeBuffers.Push(_al.GenBuffer());

        ThrowOnError("create the playback source");

        _pump = new Thread(Pump) { IsBackground = true, Name = "OpenAL audio stream" };
        _pump.Start();
    }

    /// <inheritdoc/>
    public AudioPlaybackState State
    {
        get
        {
            lock (_lock)
                return _state;
        }
    }

    /// <inheritdoc/>
    public TimeSpan Duration { get; private set; }

    /// <inheritdoc/>
    public TimeSpan Position
    {
        get
        {
            lock (_lock)
                return _clip is null ? TimeSpan.Zero : ToTime(GetPositionFrame());
        }
        set
        {
            lock (_lock)
            {
                if (_clip is not null)
                    _pendingSeek = Math.Clamp((long)(value.TotalSeconds * _clip.Format.SampleRate), 0, Math.Max(0, _clip.FrameCount));
            }
        }
    }

    /// <inheritdoc/>
    public float Volume
    {
        get
        {
            lock (_lock)
            {
                _al.GetSourceProperty(_source, SourceFloat.Gain, out float gain);
                return gain;
            }
        }
        set
        {
            lock (_lock)
                _al.SetSourceProperty(_source, SourceFloat.Gain, Math.Clamp(value, 0.0f, 1.0f));
        }
    }

    /// <inheritdoc/>
    public bool IsLooping { get; set; }

    /// <inheritdoc/>
    public void Load(AudioClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            int channels = clip.Format.Channels;
            int outputChannels = Math.Min(channels, 2);

            _clip = clip;
            _state = AudioPlaybackState.Stopped;
            _pendingSeek = null;
            _bufferFormat = outputChannels == 1 ? BufferFormat.Mono16 : BufferFormat.Stereo16;
            _bufferFrames = clip.Format.SampleRate * BufferMilliseconds / 1000;
            _converted = new short[_bufferFrames * channels];
            _output = channels > 2 ? new short[_bufferFrames * outputChannels] : _converted;
            Duration = clip.Duration;

            Restart(0);
        }
    }

    /// <inheritdoc/>
    public void Play()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            if (_clip is null || _state == AudioPlaybackState.Playing)
                return;

            _state = AudioPlaybackState.Playing;

            if (_pendingSeek is null)
                _al.SourcePlay(_source);
        }
    }

    /// <inheritdoc/>
    public void Pause()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            if (_state != AudioPlaybackState.Playing)
                return;

            _state = AudioPlaybackState.Paused;
            _al.SourcePause(_source);
        }
    }

    /// <inheritdoc/>
    public void Stop()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            if (_clip is null)
                return;

            _state = AudioPlaybackState.Stopped;
            _al.SourceStop(_source);
            _pendingSeek = 0;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _pump.Join();

        lock (_lock)
        {
            _al.SourceStop(_source);
            _al.SetSourceProperty(_source, SourceInteger.Buffer, 0);
            _al.DeleteSource(_source);

            foreach (QueuedBuffer queued in _queuedBuffers)
                _freeBuffers.Push(queued.Buffer);

            foreach (uint buffer in _freeBuffers)
                _al.DeleteBuffer(buffer);

            _decoder?.Dispose();
        }

        _device.Release();
    }

    private void Pump()
    {
        while (!_disposed)
        {
            lock (_lock)
            {
                if (!_disposed && _clip is not null)
                    Update();
            }

            Thread.Sleep(PumpIntervalMilliseconds);
        }
    }

    private void Update()
    {
        if (_pendingSeek is { } frame)
        {
            _pendingSeek = null;
            Restart(frame);
        }

        if (_state != AudioPlaybackState.Playing)
            return;

        RecycleProcessedBuffers();
        Fill();

        _al.GetSourceProperty(_source, GetSourceInteger.SourceState, out int sourceState);

        if ((SourceState)sourceState == SourceState.Playing)
            return;

        if (_queuedBuffers.Count > 0)
        {
            _al.SourcePlay(_source);
        }
        else if (_endOfStream)
        {
            _state = AudioPlaybackState.Stopped;
            Restart(0);
        }
    }

    private void Restart(long frame)
    {
        _al.SourceStop(_source);
        _al.SetSourceProperty(_source, SourceInteger.Buffer, 0);

        foreach (QueuedBuffer queued in _queuedBuffers)
            _freeBuffers.Push(queued.Buffer);

        _queuedBuffers.Clear();
        _decoder?.Dispose();
        _decoder = _clip!.OpenDecoder(frame);
        _decoded = new byte[_bufferFrames * _decoder.BytesPerFrame];
        _nextFrame = frame;
        _endOfStream = false;

        Fill();

        if (_state == AudioPlaybackState.Playing)
            _al.SourcePlay(_source);
    }

    private void RecycleProcessedBuffers()
    {
        _al.GetSourceProperty(_source, GetSourceInteger.BuffersProcessed, out int processed);

        for (int i = 0; i < processed; i++)
        {
            uint buffer;
            _al.SourceUnqueueBuffers(_source, 1, &buffer);
            _queuedBuffers.Dequeue();
            _freeBuffers.Push(buffer);
        }
    }

    private void Fill()
    {
        while (_freeBuffers.Count > 0 && !_endOfStream)
        {
            long startFrame = _nextFrame;
            int frames = ReadFrames();

            if (frames == 0 && IsLooping && startFrame > 0)
            {
                _decoder!.Dispose();
                _decoder = _clip!.OpenDecoder(0);
                startFrame = 0;
                frames = ReadFrames();
            }

            if (frames == 0)
            {
                _endOfStream = true;
                return;
            }

            uint buffer = _freeBuffers.Pop();
            int sampleCount = frames * (_bufferFormat == BufferFormat.Mono16 ? 1 : 2);

            fixed (short* pointer = _output)
                _al.BufferData(buffer, _bufferFormat, pointer, sampleCount * sizeof(short), _clip!.Format.SampleRate);

            _al.SourceQueueBuffers(_source, 1, &buffer);
            _queuedBuffers.Enqueue(new QueuedBuffer(buffer, startFrame, frames));
            _nextFrame = startFrame + frames;
        }
    }

    private int ReadFrames()
    {
        AudioDecoder decoder = _decoder!;
        int channels = decoder.Format.Channels;
        int bytesPerFrame = decoder.BytesPerFrame;
        int frames = 0;

        while (frames < _bufferFrames)
        {
            int read = decoder.Read(_decoded.AsSpan(frames * bytesPerFrame));

            if (read == 0)
                break;

            frames += read;
        }

        SampleConverter.Convert(_decoded.AsSpan(0, frames * bytesPerFrame), decoder.SampleFormat, MemoryMarshal.AsBytes(_converted.AsSpan()), SampleFormat.Int16);

        if (channels > 2)
        {
            for (int frame = 0; frame < frames; frame++)
            {
                _output[frame * 2] = _converted[frame * channels];
                _output[(frame * 2) + 1] = _converted[(frame * channels) + 1];
            }
        }

        return frames;
    }

    private long GetPositionFrame()
    {
        if (_pendingSeek is { } pending)
            return pending;

        _al.GetSourceProperty(_source, GetSourceInteger.SampleOffset, out int offset);

        foreach (QueuedBuffer queued in _queuedBuffers)
        {
            if (offset < queued.FrameCount)
                return queued.StartFrame + offset;

            offset -= queued.FrameCount;
        }

        return _nextFrame;
    }

    private TimeSpan ToTime(long frame) => TimeSpan.FromSeconds((double)frame / _clip!.Format.SampleRate);

    private void ThrowOnError(string operation)
    {
        AudioError error = _al.GetError();

        if (error != AudioError.NoError)
            throw new AudioException($"OpenAL failed to {operation}: {error}.");
    }
}
