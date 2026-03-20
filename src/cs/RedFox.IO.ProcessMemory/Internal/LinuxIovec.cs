// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using System.Runtime.InteropServices;

namespace RedFox.IO.ProcessMemory.Internal
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct LinuxIovec(nint @base, nuint length)
    {
        public nint Base = @base;

        public nuint Length = length;
    }
}
