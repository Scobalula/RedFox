// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

namespace RedFox.IO.ProcessMemory.Internal
{
    internal interface IProcessMemoryBackend : IDisposable
    {
        int ProcessId { get; }

        void Read(nint address, Span<byte> destination);

        void Write(nint address, ReadOnlySpan<byte> source);

        ProcessModuleInfo[] GetModules();

        ProcessModuleInfo GetMainModule();
    }
}
