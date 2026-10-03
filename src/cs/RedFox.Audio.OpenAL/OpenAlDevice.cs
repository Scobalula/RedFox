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

internal sealed unsafe class OpenAlDevice
{
    private static readonly Lock SharedLock = new();
    private static OpenAlDevice? _shared;

    private readonly ALContext _context;
    private readonly Device* _device;
    private readonly Context* _deviceContext;
    private int _references;

    public AL Al { get; }

    private OpenAlDevice()
    {
        _context = ALContext.GetApi(true);
        Al = AL.GetApi(true);
        _device = _context.OpenDevice(string.Empty);

        if (_device is null)
        {
            Al.Dispose();
            _context.Dispose();
            throw new AudioException("No OpenAL output device is available.");
        }

        _deviceContext = _context.CreateContext(_device, null);
        _context.MakeContextCurrent(_deviceContext);
    }

    public static OpenAlDevice Acquire()
    {
        lock (SharedLock)
        {
            _shared ??= new OpenAlDevice();
            _shared._references++;
            return _shared;
        }
    }

    public void Release()
    {
        lock (SharedLock)
        {
            if (--_references > 0)
                return;

            _context.MakeContextCurrent(null);
            _context.DestroyContext(_deviceContext);
            _context.CloseDevice(_device);
            Al.Dispose();
            _context.Dispose();
            _shared = null;
        }
    }
}
