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
    internal sealed class LinuxProcessMemoryBackend : IProcessMemoryBackend
    {
        private bool _disposed;

        public int ProcessId { get; }

        public LinuxProcessMemoryBackend(int processId)
        {
            if (!OperatingSystem.IsLinux())
            {
                throw ProcessMemoryErrors.CreateUnsupportedPlatformException("access");
            }

            ProcessMemoryValidation.ThrowIfInvalidProcessId(processId);
            ProcessId = processId;
        }

        public unsafe void Read(nint address, Span<byte> destination)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            fixed (byte* destinationPtr = destination)
            {
                LinuxIovec local = new((nint)destinationPtr, (nuint)destination.Length);
                LinuxIovec remote = new(address, (nuint)destination.Length);
                nint bytesRead = LinuxProcessNativeMethods.ProcessVmReadv(ProcessId, &local, 1, &remote, 1, 0);

                if (bytesRead == -1)
                {
                    int errorCode = Marshal.GetLastPInvokeError();
                    throw ProcessMemoryErrors.CreateNativeFailure("process_vm_readv", ProcessId, address, destination.Length, errorCode);
                }

                if (bytesRead != destination.Length)
                {
                    throw ProcessMemoryErrors.CreatePartialTransfer("read", ProcessId, address, destination.Length, bytesRead);
                }
            }
        }

        public unsafe void Write(nint address, ReadOnlySpan<byte> source)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            fixed (byte* sourcePtr = source)
            {
                LinuxIovec local = new((nint)sourcePtr, (nuint)source.Length);
                LinuxIovec remote = new(address, (nuint)source.Length);
                nint bytesWritten = LinuxProcessNativeMethods.ProcessVmWritev(ProcessId, &local, 1, &remote, 1, 0);

                if (bytesWritten == -1)
                {
                    int errorCode = Marshal.GetLastPInvokeError();
                    throw ProcessMemoryErrors.CreateNativeFailure("process_vm_writev", ProcessId, address, source.Length, errorCode);
                }

                if (bytesWritten != source.Length)
                {
                    throw ProcessMemoryErrors.CreatePartialTransfer("write", ProcessId, address, source.Length, bytesWritten);
                }
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
            _disposed = true;
        }
    }
}
