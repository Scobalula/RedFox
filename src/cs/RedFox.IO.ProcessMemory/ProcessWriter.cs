// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using RedFox.IO.ProcessMemory.Internal;

namespace RedFox.IO.ProcessMemory
{
    /// <summary>
    /// Provides memory writing functionality for a target process.
    /// </summary>
    public sealed class ProcessWriter : IDisposable
    {
        private const bool DefaultNullTerminate = true;

        private readonly IProcessMemoryBackend _backend;

        private bool _disposed;

        /// <summary>
        /// Gets the identifier of the target process.
        /// </summary>
        public int ProcessId => _backend.ProcessId;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProcessWriter"/> class for the provided process identifier.
        /// </summary>
        /// <param name="processId">The process identifier of the target process.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="processId"/> is less than or equal to zero.
        /// </exception>
        /// <exception cref="PlatformNotSupportedException">
        /// Thrown when process memory access is unsupported on the current OS.
        /// </exception>
        public ProcessWriter(int processId)
        {
            _backend = ProcessMemoryBackendFactory.Create(processId);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProcessWriter"/> class for the provided process.
        /// </summary>
        /// <param name="process">The process to write memory to.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="process"/> is null.</exception>
        public ProcessWriter(Process process)
            : this(process?.Id ?? throw new ArgumentNullException(nameof(process)))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProcessWriter"/> class
        /// for a single process with the given name.
        /// </summary>
        /// <param name="processName">
        /// The process name to locate. This must resolve to exactly one running process.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="processName"/> is null, empty, or whitespace.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when no process with the given name exists or when multiple matching processes are found.
        /// </exception>
        public ProcessWriter(string processName)
            : this(ProcessFinder.GetSingleProcessIdByName(processName))
        {
        }

        /// <summary>
        /// Writes the provided source buffer to process memory at the specified address.
        /// </summary>
        /// <param name="address">The target memory address to write to.</param>
        /// <param name="source">The source buffer containing bytes to write.</param>
        /// <exception cref="ObjectDisposedException">Thrown when this writer has already been disposed.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="address"/> is zero.</exception>
        public void Write(nint address, ReadOnlySpan<byte> source)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ProcessMemoryValidation.ThrowIfInvalidAddress(address);

            if (source.IsEmpty)
            {
                return;
            }

            _backend.Write(address, source);
        }

        /// <summary>
        /// Writes an unmanaged value to process memory at the specified address.
        /// </summary>
        /// <typeparam name="T">The unmanaged type to write.</typeparam>
        /// <param name="address">The target memory address to write to.</param>
        /// <param name="value">The value to write.</param>
        /// <exception cref="ObjectDisposedException">Thrown when this writer has already been disposed.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="address"/> is zero.</exception>
        public void Write<T>(nint address, T value) where T : unmanaged
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ProcessMemoryValidation.ThrowIfInvalidAddress(address);

            Span<byte> buffer = stackalloc byte[Unsafe.SizeOf<T>()];
            MemoryMarshal.Write(buffer, in value);
            _backend.Write(address, buffer);
        }

        /// <summary>
        /// Writes a byte array to process memory at the specified address.
        /// </summary>
        /// <param name="address">The target memory address to write to.</param>
        /// <param name="bytes">The bytes to write.</param>
        /// <exception cref="ObjectDisposedException">Thrown when this writer has already been disposed.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="address"/> is zero.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="bytes"/> is null.</exception>
        public void WriteBytes(nint address, byte[] bytes)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ProcessMemoryValidation.ThrowIfInvalidAddress(address);
            ArgumentNullException.ThrowIfNull(bytes);

            if (bytes.Length == 0)
            {
                return;
            }

            _backend.Write(address, bytes);
        }

        /// <summary>
        /// Writes a 16-bit signed integer to process memory.
        /// </summary>
        /// <param name="address">The target memory address to write to.</param>
        /// <param name="value">The 16-bit signed integer value to write.</param>
        public void WriteInt16(nint address, short value) => Write(address, value);

        /// <summary>
        /// Writes a 16-bit unsigned integer to process memory.
        /// </summary>
        /// <param name="address">The target memory address to write to.</param>
        /// <param name="value">The 16-bit unsigned integer value to write.</param>
        public void WriteUInt16(nint address, ushort value) => Write(address, value);

        /// <summary>
        /// Writes a 32-bit signed integer to process memory.
        /// </summary>
        /// <param name="address">The target memory address to write to.</param>
        /// <param name="value">The 32-bit signed integer value to write.</param>
        public void WriteInt32(nint address, int value) => Write(address, value);

        /// <summary>
        /// Writes a 32-bit unsigned integer to process memory.
        /// </summary>
        /// <param name="address">The target memory address to write to.</param>
        /// <param name="value">The 32-bit unsigned integer value to write.</param>
        public void WriteUInt32(nint address, uint value) => Write(address, value);

        /// <summary>
        /// Writes a 64-bit signed integer to process memory.
        /// </summary>
        /// <param name="address">The target memory address to write to.</param>
        /// <param name="value">The 64-bit signed integer value to write.</param>
        public void WriteInt64(nint address, long value) => Write(address, value);

        /// <summary>
        /// Writes a 64-bit unsigned integer to process memory.
        /// </summary>
        /// <param name="address">The target memory address to write to.</param>
        /// <param name="value">The 64-bit unsigned integer value to write.</param>
        public void WriteUInt64(nint address, ulong value) => Write(address, value);

        /// <summary>
        /// Writes a 32-bit single-precision floating-point value to process memory.
        /// </summary>
        /// <param name="address">The target memory address to write to.</param>
        /// <param name="value">The single-precision floating-point value to write.</param>
        public void WriteSingle(nint address, float value) => Write(address, value);

        /// <summary>
        /// Writes a 64-bit double-precision floating-point value to process memory.
        /// </summary>
        /// <param name="address">The target memory address to write to.</param>
        /// <param name="value">The double-precision floating-point value to write.</param>
        public void WriteDouble(nint address, double value) => Write(address, value);

        /// <summary>
        /// Writes a pointer value to process memory using the specified pointer width.
        /// </summary>
        /// <param name="address">The target memory address to write to.</param>
        /// <param name="pointerValue">The pointer value to write.</param>
        /// <param name="pointerSize">The pointer width to use for the write operation.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="pointerSize"/> is invalid.</exception>
        public void WritePointer(nint address, nint pointerValue)
        {
            WritePointer(address, pointerValue, ProcessPointerSize.Native);
        }

        /// <summary>
        /// Writes a pointer value to process memory using the specified pointer width.
        /// </summary>
        /// <param name="address">The target memory address to write to.</param>
        /// <param name="pointerValue">The pointer value to write.</param>
        /// <param name="pointerSize">The pointer width to use for the write operation.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="pointerSize"/> is invalid.</exception>
        public void WritePointer(
            nint address,
            nint pointerValue,
            ProcessPointerSize pointerSize)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ProcessMemoryValidation.ThrowIfInvalidAddress(address);

            switch (pointerSize)
            {
                case ProcessPointerSize.Native:
                {
                    if (IntPtr.Size == 8)
                    {
                        ulong pointerAsUnsigned = unchecked((ulong)pointerValue);
                        Write(address, pointerAsUnsigned);
                    }
                    else
                    {
                        uint pointerAsUnsigned = unchecked((uint)(ulong)pointerValue);
                        Write(address, pointerAsUnsigned);
                    }

                    break;
                }
                case ProcessPointerSize.Bit32:
                {
                    uint pointerAsUnsigned = unchecked((uint)(ulong)pointerValue);
                    Write(address, pointerAsUnsigned);
                    break;
                }
                case ProcessPointerSize.Bit64:
                {
                    ulong pointerAsUnsigned = unchecked((ulong)pointerValue);
                    Write(address, pointerAsUnsigned);
                    break;
                }
                default:
                {
                    throw new ArgumentOutOfRangeException(nameof(pointerSize), pointerSize, "Pointer size is not valid.");
                }
            }
        }

        /// <summary>
        /// Writes a string to process memory using the provided encoding.
        /// </summary>
        /// <param name="address">The target memory address to write to.</param>
        /// <param name="value">The string value to write.</param>
        /// <param name="encoding">The encoding used to convert the string to bytes.</param>
        /// <param name="nullTerminate">
        /// <see langword="true"/> to append an encoding-aware null terminator; otherwise, <see langword="false"/>.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="value"/> or <paramref name="encoding"/> is null.
        /// </exception>
        public void WriteString(nint address, string value, Encoding encoding)
        {
            WriteString(address, value, encoding, DefaultNullTerminate);
        }

        /// <summary>
        /// Writes a string to process memory using the provided encoding.
        /// </summary>
        /// <param name="address">The target memory address to write to.</param>
        /// <param name="value">The string value to write.</param>
        /// <param name="encoding">The encoding used to convert the string to bytes.</param>
        /// <param name="nullTerminate">
        /// <see langword="true"/> to append an encoding-aware null terminator; otherwise, <see langword="false"/>.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="value"/> or <paramref name="encoding"/> is null.
        /// </exception>
        public void WriteString(nint address, string value, Encoding encoding, bool nullTerminate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ProcessMemoryValidation.ThrowIfInvalidAddress(address);
            ArgumentNullException.ThrowIfNull(value);
            ArgumentNullException.ThrowIfNull(encoding);

            byte[] valueBytes = encoding.GetBytes(value);
            if (!nullTerminate)
            {
                Write(address, valueBytes);
                return;
            }

            byte[] terminatorBytes = encoding.GetBytes("\0");
            byte[] output = new byte[valueBytes.Length + terminatorBytes.Length];
            Buffer.BlockCopy(valueBytes, 0, output, 0, valueBytes.Length);
            Buffer.BlockCopy(terminatorBytes, 0, output, valueBytes.Length, terminatorBytes.Length);
            Write(address, output);
        }

        /// <summary>
        /// Writes a UTF-8 string to process memory.
        /// </summary>
        /// <param name="address">The target memory address to write to.</param>
        /// <param name="value">The string value to write.</param>
        /// <param name="nullTerminate">
        /// <see langword="true"/> to append a null terminator; otherwise, <see langword="false"/>.
        /// </param>
        public void WriteUtf8String(nint address, string value)
        {
            WriteUtf8String(address, value, DefaultNullTerminate);
        }

        /// <summary>
        /// Writes a UTF-8 string to process memory.
        /// </summary>
        /// <param name="address">The target memory address to write to.</param>
        /// <param name="value">The string value to write.</param>
        /// <param name="nullTerminate">
        /// <see langword="true"/> to append a null terminator; otherwise, <see langword="false"/>.
        /// </param>
        public void WriteUtf8String(nint address, string value, bool nullTerminate)
        {
            WriteString(address, value, Encoding.UTF8, nullTerminate);
        }

        /// <summary>
        /// Writes an ASCII string to process memory.
        /// </summary>
        /// <param name="address">The target memory address to write to.</param>
        /// <param name="value">The string value to write.</param>
        /// <param name="nullTerminate">
        /// <see langword="true"/> to append a null terminator; otherwise, <see langword="false"/>.
        /// </param>
        public void WriteAsciiString(nint address, string value)
        {
            WriteAsciiString(address, value, DefaultNullTerminate);
        }

        /// <summary>
        /// Writes an ASCII string to process memory.
        /// </summary>
        /// <param name="address">The target memory address to write to.</param>
        /// <param name="value">The string value to write.</param>
        /// <param name="nullTerminate">
        /// <see langword="true"/> to append a null terminator; otherwise, <see langword="false"/>.
        /// </param>
        public void WriteAsciiString(nint address, string value, bool nullTerminate)
        {
            WriteString(address, value, Encoding.ASCII, nullTerminate);
        }

        /// <summary>
        /// Writes a UTF-16 little-endian string to process memory.
        /// </summary>
        /// <param name="address">The target memory address to write to.</param>
        /// <param name="value">The string value to write.</param>
        /// <param name="nullTerminate">
        /// <see langword="true"/> to append a null terminator; otherwise, <see langword="false"/>.
        /// </param>
        public void WriteUtf16String(nint address, string value)
        {
            WriteUtf16String(address, value, DefaultNullTerminate);
        }

        /// <summary>
        /// Writes a UTF-16 little-endian string to process memory.
        /// </summary>
        /// <param name="address">The target memory address to write to.</param>
        /// <param name="value">The string value to write.</param>
        /// <param name="nullTerminate">
        /// <see langword="true"/> to append a null terminator; otherwise, <see langword="false"/>.
        /// </param>
        public void WriteUtf16String(nint address, string value, bool nullTerminate)
        {
            WriteString(address, value, Encoding.Unicode, nullTerminate);
        }

        /// <summary>
        /// Reads all modules loaded in the target process.
        /// </summary>
        /// <returns>Module metadata sorted by the runtime process API ordering.</returns>
        public ProcessModuleInfo[] GetModules()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _backend.GetModules();
        }

        /// <summary>
        /// Reads the main module metadata for the target process.
        /// </summary>
        /// <returns>The main module metadata.</returns>
        public ProcessModuleInfo GetMainModule()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _backend.GetMainModule();
        }

        /// <summary>
        /// Reads module metadata by module name.
        /// </summary>
        /// <param name="moduleName">The module name or file name to resolve.</param>
        /// <returns>The resolved module metadata.</returns>
        public ProcessModuleInfo GetModule(string moduleName)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ProcessModuleInfo[] modules = _backend.GetModules();
            ProcessModuleInfo mainModule = _backend.GetMainModule();
            return ProcessModuleResolver.GetModuleByName(modules, moduleName, ProcessId, mainModule);
        }

        /// <summary>
        /// Gets the base address of the named module.
        /// </summary>
        /// <param name="moduleName">The module name or file name to resolve.</param>
        /// <returns>The module base address.</returns>
        public nint GetModuleBaseAddress(string moduleName) => GetModule(moduleName).BaseAddress;

        /// <summary>
        /// Gets the base address of the process main module.
        /// </summary>
        /// <returns>The main module base address.</returns>
        public nint GetMainModuleBaseAddress() => GetMainModule().BaseAddress;

        /// <summary>
        /// Releases process resources held by this writer.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _backend.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Creates a new <see cref="ProcessWriter"/> for a single process that matches the provided name.
        /// </summary>
        /// <param name="processName">
        /// The process name to locate. This must resolve to exactly one running process.
        /// </param>
        /// <returns>A configured <see cref="ProcessWriter"/> instance.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="processName"/> is null, empty, or whitespace.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when no process with the given name exists or when multiple matching processes are found.
        /// </exception>
        public static ProcessWriter OpenByName(string processName) =>
            new(processName);

        /// <summary>
        /// Gets the process identifiers that match the provided process name.
        /// </summary>
        /// <param name="processName">The process name to locate.</param>
        /// <returns>An array of matching process identifiers.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="processName"/> is null, empty, or whitespace.
        /// </exception>
        public static int[] FindProcessIdsByName(string processName) =>
            ProcessFinder.FindProcessIdsByName(processName);

        /// <summary>
        /// Gets running processes that match the provided process name.
        /// </summary>
        /// <param name="processName">The process name to locate.</param>
        /// <returns>Matching process objects sorted by process ID in ascending order. Caller owns disposal.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="processName"/> is null, empty, or whitespace.
        /// </exception>
        public static Process[] FindProcessesByName(string processName) =>
            ProcessFinder.FindProcessesByName(processName);
    }
}
