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
    internal sealed class WindowsProcessMemoryBackend : IProcessMemoryBackend
    {
        private readonly SafeProcessHandle _processHandle;

        private bool _disposed;

        public int ProcessId { get; }

        public WindowsProcessMemoryBackend(int processId)
        {
            if (!OperatingSystem.IsWindows())
            {
                throw ProcessMemoryErrors.CreateUnsupportedPlatformException("access");
            }

            ProcessMemoryValidation.ThrowIfInvalidProcessId(processId);
            ProcessId = processId;
            _processHandle = WindowsProcessNativeMethods.OpenProcessHandle(processId);
        }

        public void Read(nint address, Span<byte> destination)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            bool success = WindowsProcessNativeMethods.ReadProcessMemory(_processHandle, address, destination, (nuint)destination.Length, out nint bytesRead);

            if (!success)
            {
                int errorCode = Marshal.GetLastPInvokeError();
                throw ProcessMemoryErrors.CreateNativeFailure("ReadProcessMemory", ProcessId, address, destination.Length, errorCode);
            }

            if (bytesRead != destination.Length)
            {
                throw ProcessMemoryErrors.CreatePartialTransfer("read", ProcessId, address, destination.Length, bytesRead);
            }
        }

        public void Write(nint address, ReadOnlySpan<byte> source)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            bool success = WindowsProcessNativeMethods.WriteProcessMemory(_processHandle, address, source, (nuint)source.Length, out nint bytesWritten);

            if (!success)
            {
                int errorCode = Marshal.GetLastPInvokeError();
                throw ProcessMemoryErrors.CreateNativeFailure("WriteProcessMemory", ProcessId, address, source.Length, errorCode);
            }

            if (bytesWritten != source.Length)
            {
                throw ProcessMemoryErrors.CreatePartialTransfer("write", ProcessId, address, source.Length, bytesWritten);
            }
        }

        public ProcessModuleInfo[] GetModules()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return ProcessFinder.GetProcessModules(ProcessId);
        }

        public ProcessModuleInfo GetMainModule()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return ProcessFinder.GetMainModule(ProcessId);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _processHandle.Dispose();
            _disposed = true;
        }
    }
}
