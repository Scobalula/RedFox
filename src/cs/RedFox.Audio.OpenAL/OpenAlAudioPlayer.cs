// --------------------------------------------------------------------------------------
// RedFox Utility Library - MIT License
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

using Silk.NET.OpenAL;

namespace RedFox.Audio.OpenAL;

/// <summary>
/// Plays <see cref="AudioBuffer"/> instances through an OpenAL Soft device and context.
/// Buffers with more than two channels are downmixed to their first two channels.
/// </summary>
public sealed unsafe class OpenAlAudioPlayer : IAudioPlayer
{
    private readonly AL _al;
    private readonly ALContext _context;
    private readonly Device* _device;
    private readonly Context* _deviceContext;
    private readonly uint _source;
    private uint _buffer;
    private bool _disposed;

    /// <summary>
    /// Opens the default OpenAL output device and creates a playback source.
    /// </summary>
    /// <exception cref="AudioException">Thrown when no output device can be opened.</exception>
    public OpenAlAudioPlayer()
    {
        _context = ALContext.GetApi(true);
        _al = AL.GetApi(true);
        _device = _context.OpenDevice(string.Empty);

        if (_device is null)
        {
            throw new AudioException("No OpenAL output device is available.");
        }

        _deviceContext = _context.CreateContext(_device, null);
        _context.MakeContextCurrent(_deviceContext);
        _source = _al.GenSource();
        ThrowOnError("create the playback source");
    }

    /// <inheritdoc/>
    public AudioPlaybackState State
    {
        get
        {
            _al.GetSourceProperty(_source, GetSourceInteger.SourceState, out int state);
            return (SourceState)state switch
            {
                SourceState.Playing => AudioPlaybackState.Playing,
                SourceState.Paused => AudioPlaybackState.Paused,
                _ => AudioPlaybackState.Stopped,
            };
        }
    }

    /// <inheritdoc/>
    public TimeSpan Duration { get; private set; }

    /// <inheritdoc/>
    public TimeSpan Position
    {
        get
        {
            _al.GetSourceProperty(_source, SourceFloat.SecOffset, out float seconds);
            return TimeSpan.FromSeconds(seconds);
        }
        set => _al.SetSourceProperty(_source, SourceFloat.SecOffset, (float)Math.Clamp(value.TotalSeconds, 0.0, Duration.TotalSeconds));
    }

    /// <inheritdoc/>
    public float Volume
    {
        get
        {
            _al.GetSourceProperty(_source, SourceFloat.Gain, out float gain);
            return gain;
        }
        set => _al.SetSourceProperty(_source, SourceFloat.Gain, Math.Clamp(value, 0.0f, 1.0f));
    }

    /// <inheritdoc/>
    public bool IsLooping
    {
        get
        {
            _al.GetSourceProperty(_source, SourceBoolean.Looping, out bool looping);
            return looping;
        }
        set => _al.SetSourceProperty(_source, SourceBoolean.Looping, value);
    }

    /// <inheritdoc/>
    public void Load(AudioBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (buffer.Channels <= 0 || buffer.SampleRate <= 0)
        {
            throw new AudioException("Audio buffers need a positive channel count and sample rate.");
        }

        _al.SourceStop(_source);
        _al.SetSourceProperty(_source, SourceInteger.Buffer, 0);
        ReleaseBuffer();

        ReadOnlySpan<short> samples = buffer.Channels > 2 ? DownmixToStereo(buffer) : buffer.Samples.Span;
        BufferFormat format = buffer.Channels == 1 ? BufferFormat.Mono16 : BufferFormat.Stereo16;

        _buffer = _al.GenBuffer();
        fixed (short* pointer = samples)
        {
            _al.BufferData(_buffer, format, pointer, samples.Length * sizeof(short), buffer.SampleRate);
        }

        ThrowOnError("upload the audio buffer");
        _al.SetSourceProperty(_source, SourceInteger.Buffer, _buffer);
        Duration = buffer.Duration;
    }

    /// <inheritdoc/>
    public void Play()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_buffer != 0)
        {
            _al.SourcePlay(_source);
        }
    }

    /// <inheritdoc/>
    public void Pause()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _al.SourcePause(_source);
    }

    /// <inheritdoc/>
    public void Stop()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _al.SourceStop(_source);
        _al.SetSourceProperty(_source, SourceFloat.SecOffset, 0.0f);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _al.SourceStop(_source);
        _al.DeleteSource(_source);
        ReleaseBuffer();
        _context.MakeContextCurrent(null);
        _context.DestroyContext(_deviceContext);
        _context.CloseDevice(_device);
        _al.Dispose();
        _context.Dispose();
    }

    private static short[] DownmixToStereo(AudioBuffer buffer)
    {
        ReadOnlySpan<short> source = buffer.Samples.Span;
        int frameCount = source.Length / buffer.Channels;
        short[] stereo = new short[frameCount * 2];

        for (int frame = 0; frame < frameCount; frame++)
        {
            stereo[frame * 2] = source[frame * buffer.Channels];
            stereo[(frame * 2) + 1] = source[(frame * buffer.Channels) + 1];
        }

        return stereo;
    }

    private void ReleaseBuffer()
    {
        if (_buffer != 0)
        {
            _al.DeleteBuffer(_buffer);
            _buffer = 0;
        }

        Duration = TimeSpan.Zero;
    }

    private void ThrowOnError(string operation)
    {
        AudioError error = _al.GetError();
        if (error != AudioError.NoError)
        {
            throw new AudioException($"OpenAL failed to {operation}: {error}.");
        }
    }
}
