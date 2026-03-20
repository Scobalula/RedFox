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
using RedFox;
using RedFox.IO.ProcessMemory.Internal;

namespace RedFox.IO.ProcessMemory
{
    /// <summary>
    /// Provides memory reading functionality for a target process.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="ProcessReader"/> class for the provided process identifier.
    /// </remarks>
    /// <param name="processId">The process identifier of the target process.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="processId"/> is less than or equal to zero.
    /// </exception>
    /// <exception cref="PlatformNotSupportedException">
    /// Thrown when process memory access is unsupported on the current OS.
    /// </exception>
    public sealed class ProcessReader(int processId) : IDisposable
    {
        private const int DefaultStringReadChunkSize = 0x80;
        private const int DefaultStringReadMaxBytes = 4096;
        private const int DefaultScanChunkSize = 0x10000;

        private readonly IProcessMemoryBackend _backend = ProcessMemoryBackendFactory.Create(processId);

        private bool _disposed;

        /// <summary>
        /// Gets the identifier of the target process.
        /// </summary>
        public int ProcessId => _backend.ProcessId;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProcessReader"/> class for the provided process.
        /// </summary>
        /// <param name="process">The process to read memory from.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="process"/> is null.</exception>
        public ProcessReader(Process process) : this(process?.Id ?? throw new ArgumentNullException(nameof(process)))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProcessReader"/> class for a single matching process name.
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
        public ProcessReader(string processName) : this(ProcessFinder.GetSingleProcessIdByName(processName))
        {
        }

        /// <summary>
        /// Reads memory from the process into the supplied destination buffer.
        /// </summary>
        /// <param name="address">The target memory address to read from.</param>
        /// <param name="destination">The destination buffer that receives the read bytes.</param>
        /// <exception cref="ObjectDisposedException">Thrown when this reader has already been disposed.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="address"/> is zero.</exception>
        public void Read(nint address, Span<byte> destination)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ProcessMemoryValidation.ThrowIfInvalidAddress(address);

            if (destination.IsEmpty)
            {
                return;
            }

            _backend.Read(address, destination);
        }

        /// <summary>
        /// Reads an unmanaged value from the target process.
        /// </summary>
        /// <typeparam name="T">The unmanaged type to read.</typeparam>
        /// <param name="address">The target memory address to read from.</param>
        /// <returns>The value read from process memory.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when this reader has already been disposed.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="address"/> is zero.</exception>
        public T Read<T>(nint address) where T : unmanaged
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ProcessMemoryValidation.ThrowIfInvalidAddress(address);

            Span<byte> buffer = stackalloc byte[Unsafe.SizeOf<T>()];
            _backend.Read(address, buffer);
            return MemoryMarshal.Read<T>(buffer);
        }

        /// <summary>
        /// Reads a byte array from the target process.
        /// </summary>
        /// <param name="address">The target memory address to read from.</param>
        /// <param name="count">The number of bytes to read.</param>
        /// <returns>The bytes read from process memory.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when this reader has already been disposed.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="address"/> is zero or when <paramref name="count"/> is negative.
        /// </exception>
        public byte[] ReadBytes(nint address, int count)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ProcessMemoryValidation.ThrowIfInvalidAddress(address);
            ArgumentOutOfRangeException.ThrowIfNegative(count);

            if (count == 0)
            {
                return [];
            }

            byte[] buffer = new byte[count];
            _backend.Read(address, buffer);
            return buffer;
        }

        /// <summary>
        /// Reads a 16-bit signed integer from process memory.
        /// </summary>
        /// <param name="address">The target memory address to read from.</param>
        /// <returns>The 16-bit signed integer value.</returns>
        public short ReadInt16(nint address) => Read<short>(address);

        /// <summary>
        /// Reads a 16-bit unsigned integer from process memory.
        /// </summary>
        /// <param name="address">The target memory address to read from.</param>
        /// <returns>The 16-bit unsigned integer value.</returns>
        public ushort ReadUInt16(nint address) => Read<ushort>(address);

        /// <summary>
        /// Reads a 32-bit signed integer from process memory.
        /// </summary>
        /// <param name="address">The target memory address to read from.</param>
        /// <returns>The 32-bit signed integer value.</returns>
        public int ReadInt32(nint address) => Read<int>(address);

        /// <summary>
        /// Reads a 32-bit unsigned integer from process memory.
        /// </summary>
        /// <param name="address">The target memory address to read from.</param>
        /// <returns>The 32-bit unsigned integer value.</returns>
        public uint ReadUInt32(nint address) => Read<uint>(address);

        /// <summary>
        /// Reads a 64-bit signed integer from process memory.
        /// </summary>
        /// <param name="address">The target memory address to read from.</param>
        /// <returns>The 64-bit signed integer value.</returns>
        public long ReadInt64(nint address) => Read<long>(address);

        /// <summary>
        /// Reads a 64-bit unsigned integer from process memory.
        /// </summary>
        /// <param name="address">The target memory address to read from.</param>
        /// <returns>The 64-bit unsigned integer value.</returns>
        public ulong ReadUInt64(nint address) => Read<ulong>(address);

        /// <summary>
        /// Reads a 32-bit single-precision floating-point value from process memory.
        /// </summary>
        /// <param name="address">The target memory address to read from.</param>
        /// <returns>The single-precision floating-point value.</returns>
        public float ReadSingle(nint address) => Read<float>(address);

        /// <summary>
        /// Reads a 64-bit double-precision floating-point value from process memory.
        /// </summary>
        /// <param name="address">The target memory address to read from.</param>
        /// <returns>The double-precision floating-point value.</returns>
        public double ReadDouble(nint address) => Read<double>(address);

        /// <summary>
        /// Reads a pointer value from process memory.
        /// </summary>
        /// <param name="address">The target memory address to read from.</param>
        /// <returns>The pointer value read from process memory using the native pointer size.</returns>
        public nint ReadPointer(nint address)
        {
            return ReadPointer(address, ProcessPointerSize.Native);
        }

        /// <summary>
        /// Reads a pointer value from process memory.
        /// </summary>
        /// <param name="address">The target memory address to read from.</param>
        /// <param name="pointerSize">The pointer width to use for the read operation.</param>
        /// <returns>The pointer value read from process memory.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="pointerSize"/> is invalid.</exception>
        public nint ReadPointer(nint address, ProcessPointerSize pointerSize)
        {
            return pointerSize switch
            {
                ProcessPointerSize.Native => IntPtr.Size == 8 ? (nint)Read<ulong>(address) : (nint)(nuint)Read<uint>(address),
                ProcessPointerSize.Bit32 => (nint)(nuint)Read<uint>(address),
                ProcessPointerSize.Bit64 => (nint)Read<ulong>(address),
                _ => throw new ArgumentOutOfRangeException(nameof(pointerSize), pointerSize, "Pointer size is not valid.")
            };
        }

        /// <summary>
        /// Reads a pointer from process memory, then reads a value of the given unmanaged type at the pointed address.
        /// </summary>
        /// <typeparam name="T">The unmanaged type to read from the pointed address.</typeparam>
        /// <param name="address">The memory address that contains the pointer value.</param>
        /// <returns>The value of type <typeparamref name="T"/> at the pointed address.</returns>
        public T ReadPointer<T>(nint address) where T : unmanaged
        {
            return ReadPointer<T>(address, ProcessPointerSize.Native);
        }

        /// <summary>
        /// Reads a pointer from process memory, then reads a value of the given unmanaged type at the pointed address.
        /// </summary>
        /// <typeparam name="T">The unmanaged type to read from the pointed address.</typeparam>
        /// <param name="address">The memory address that contains the pointer value.</param>
        /// <param name="pointerSize">The pointer width to use when reading the pointer value.</param>
        /// <returns>The value of type <typeparamref name="T"/> at the pointed address.</returns>
        public T ReadPointer<T>(nint address, ProcessPointerSize pointerSize) where T : unmanaged
        {
            nint pointedAddress = ReadPointer(address, pointerSize);
            ProcessMemoryValidation.ThrowIfInvalidAddress(pointedAddress);
            return Read<T>(pointedAddress);
        }

        /// <summary>
        /// Reads a null-terminated string from process memory using the provided encoding.
        /// </summary>
        /// <param name="address">The target memory address to read from.</param>
        /// <param name="encoding">The text encoding used to decode bytes.</param>
        /// <returns>The decoded string up to, but not including, the null terminator.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="encoding"/> is null.</exception>
        public string ReadString(nint address, Encoding encoding)
        {
            return ReadString(address, encoding, DefaultStringReadMaxBytes);
        }

        /// <summary>
        /// Reads a null-terminated string from process memory using the provided encoding.
        /// </summary>
        /// <param name="address">The target memory address to read from.</param>
        /// <param name="encoding">The text encoding used to decode bytes.</param>
        /// <param name="maxBytes">
        /// The maximum number of bytes to read while searching for the null terminator.
        /// Use this as a guard against scanning unbounded memory.
        /// </param>
        /// <returns>The decoded string up to, but not including, the null terminator.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="encoding"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxBytes"/> is less than one.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when a null terminator is not found within <paramref name="maxBytes"/>.
        /// </exception>
        public string ReadString(nint address, Encoding encoding, int maxBytes)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ProcessMemoryValidation.ThrowIfInvalidAddress(address);
            ArgumentNullException.ThrowIfNull(encoding);
            if (maxBytes < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxBytes), maxBytes, "Maximum byte count must be at least one.");
            }

            int pageSize = Environment.SystemPageSize;
            if (pageSize < 1)
            {
                pageSize = DefaultStringReadChunkSize;
            }

            long currentOffset = 0;

            byte[] stringBytes = NullTerminatedStringReader.ReadNullTerminatedBytes(
                encoding,
                maxBytes,
                DefaultStringReadChunkSize,
                readChunk: destination =>
                {
                    int remainingBytes = maxBytes - (int)currentOffset;
                    if (remainingBytes <= 0 || destination.IsEmpty)
                    {
                        return 0;
                    }

                    nint currentAddress = address + (nint)currentOffset;
                    int readLength = Math.Min(destination.Length, remainingBytes);
                    readLength = Math.Min(readLength, GetBytesUntilPageBoundary(currentAddress, pageSize));
                    _backend.Read(currentAddress, destination[..readLength]);
                    currentOffset += readLength;
                    return readLength;
                },
                onMissing: () => new InvalidOperationException($"Null terminator was not found within {maxBytes} bytes at address 0x{address:X}."));

            return encoding.GetString(stringBytes);
        }

        /// <summary>
        /// Reads a null-terminated UTF-8 string from process memory.
        /// </summary>
        /// <param name="address">The target memory address to read from.</param>
        /// <returns>The decoded UTF-8 string.</returns>
        public string ReadUtf8String(nint address)
        {
            return ReadUtf8String(address, DefaultStringReadMaxBytes);
        }

        /// <summary>
        /// Reads a null-terminated UTF-8 string from process memory.
        /// </summary>
        /// <param name="address">The target memory address to read from.</param>
        /// <param name="maxBytes">The maximum number of bytes to read while searching for the null terminator.</param>
        /// <returns>The decoded UTF-8 string.</returns>
        public string ReadUtf8String(nint address, int maxBytes) => ReadString(address, Encoding.UTF8, maxBytes);

        /// <summary>
        /// Reads a null-terminated ASCII string from process memory.
        /// </summary>
        /// <param name="address">The target memory address to read from.</param>
        /// <returns>The decoded ASCII string.</returns>
        public string ReadAsciiString(nint address)
        {
            return ReadAsciiString(address, DefaultStringReadMaxBytes);
        }

        /// <summary>
        /// Reads a null-terminated ASCII string from process memory.
        /// </summary>
        /// <param name="address">The target memory address to read from.</param>
        /// <param name="maxBytes">The maximum number of bytes to read while searching for the null terminator.</param>
        /// <returns>The decoded ASCII string.</returns>
        public string ReadAsciiString(nint address, int maxBytes) => ReadString(address, Encoding.ASCII, maxBytes);

        /// <summary>
        /// Reads a null-terminated UTF-16 little-endian string from process memory.
        /// </summary>
        /// <param name="address">The target memory address to read from.</param>
        /// <returns>The decoded UTF-16 string.</returns>
        public string ReadUtf16String(nint address)
        {
            return ReadUtf16String(address, DefaultStringReadMaxBytes);
        }

        /// <summary>
        /// Reads a null-terminated UTF-16 little-endian string from process memory.
        /// </summary>
        /// <param name="address">The target memory address to read from.</param>
        /// <param name="maxBytes">The maximum number of bytes to read while searching for the null terminator.</param>
        /// <returns>The decoded UTF-16 string.</returns>
        public string ReadUtf16String(nint address, int maxBytes) => ReadString(address, Encoding.Unicode, maxBytes);

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
        /// Scans process memory for the first occurrence of a byte pattern.
        /// </summary>
        /// <param name="hexPattern">The byte pattern text, for example: <c>FF ?? ?? BA</c>.</param>
        /// <param name="startAddress">The scan start address.</param>
        /// <param name="endAddress">The exclusive scan end address.</param>
        /// <returns>The first matching address, or <see cref="IntPtr.Zero"/> when no match is found.</returns>
        public nint ScanFirst(string hexPattern, nint startAddress, nint endAddress)
        {
            return ScanFirst(hexPattern, startAddress, endAddress, DefaultScanChunkSize);
        }

        /// <summary>
        /// Scans process memory for the first occurrence of a byte pattern.
        /// </summary>
        /// <param name="hexPattern">The byte pattern text, for example: <c>FF ?? ?? BA</c>.</param>
        /// <param name="startAddress">The scan start address.</param>
        /// <param name="endAddress">The exclusive scan end address.</param>
        /// <param name="chunkSize">The chunk size used to read process memory while scanning.</param>
        /// <returns>The first matching address, or <see cref="IntPtr.Zero"/> when no match is found.</returns>
        public nint ScanFirst(string hexPattern, nint startAddress, nint endAddress, int chunkSize)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(hexPattern);
            Pattern<byte> pattern = BytePattern.Parse(hexPattern);
            return ScanFirst(pattern, startAddress, endAddress, chunkSize);
        }

        /// <summary>
        /// Scans process memory for the first occurrence of a byte pattern.
        /// </summary>
        /// <param name="pattern">The byte pattern to scan for.</param>
        /// <param name="startAddress">The scan start address.</param>
        /// <param name="endAddress">The exclusive scan end address.</param>
        /// <returns>The first matching address, or <see cref="IntPtr.Zero"/> when no match is found.</returns>
        public nint ScanFirst(Pattern<byte> pattern, nint startAddress, nint endAddress)
        {
            return ScanFirst(pattern, startAddress, endAddress, DefaultScanChunkSize);
        }

        /// <summary>
        /// Scans process memory for the first occurrence of a byte pattern.
        /// </summary>
        /// <param name="pattern">The byte pattern to scan for.</param>
        /// <param name="startAddress">The scan start address.</param>
        /// <param name="endAddress">The exclusive scan end address.</param>
        /// <param name="chunkSize">The chunk size used to read process memory while scanning.</param>
        /// <returns>The first matching address, or <see cref="IntPtr.Zero"/> when no match is found.</returns>
        public nint ScanFirst(Pattern<byte> pattern, nint startAddress, nint endAddress, int chunkSize)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ProcessMemoryValidation.ThrowIfInvalidAddress(startAddress);
            ProcessMemoryValidation.ThrowIfInvalidAddress(endAddress);
            if (endAddress <= startAddress)
            {
                throw new ArgumentOutOfRangeException(nameof(endAddress), endAddress, "End address must be greater than start address.");
            }

            if (chunkSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(chunkSize), chunkSize, "Chunk size must be greater than zero.");
            }

            long[] matches = BytePatternScanner.Scan(
                pattern,
                startAddress,
                endAddress,
                chunkSize,
                firstOnly: true,
                readChunk: (offset, destination) => ReadScanChunk((nint)offset, destination));

            return matches.Length == 0 ? IntPtr.Zero : (nint)matches[0];
        }

        /// <summary>
        /// Scans the main module for the first occurrence of a byte pattern.
        /// </summary>
        /// <param name="hexPattern">The byte pattern text, for example: <c>FF ?? ?? BA</c>.</param>
        /// <returns>The first matching address, or <see cref="IntPtr.Zero"/> when no match is found.</returns>
        public nint ScanMainModule(string hexPattern)
        {
            return ScanMainModule(hexPattern, DefaultScanChunkSize);
        }

        /// <summary>
        /// Scans the main module for the first occurrence of a byte pattern.
        /// </summary>
        /// <param name="hexPattern">The byte pattern text, for example: <c>FF ?? ?? BA</c>.</param>
        /// <param name="chunkSize">The chunk size used to read process memory while scanning.</param>
        /// <returns>The first matching address, or <see cref="IntPtr.Zero"/> when no match is found.</returns>
        public nint ScanMainModule(string hexPattern, int chunkSize)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(hexPattern);
            Pattern<byte> pattern = BytePattern.Parse(hexPattern);
            return ScanMainModule(pattern, chunkSize);
        }

        /// <summary>
        /// Scans the main module for the first occurrence of a byte pattern.
        /// </summary>
        /// <param name="pattern">The byte pattern to scan for.</param>
        /// <returns>The first matching address, or <see cref="IntPtr.Zero"/> when no match is found.</returns>
        public nint ScanMainModule(Pattern<byte> pattern)
        {
            return ScanMainModule(pattern, DefaultScanChunkSize);
        }

        /// <summary>
        /// Scans the main module for the first occurrence of a byte pattern.
        /// </summary>
        /// <param name="pattern">The byte pattern to scan for.</param>
        /// <param name="chunkSize">The chunk size used to read process memory while scanning.</param>
        /// <returns>The first matching address, or <see cref="IntPtr.Zero"/> when no match is found.</returns>
        public nint ScanMainModule(Pattern<byte> pattern, int chunkSize)
        {
            ProcessModuleInfo mainModule = GetMainModule();
            return Scan(pattern, mainModule.BaseAddress, mainModule.EndAddress, chunkSize);
        }

        /// <summary>
        /// Scans the entire main module and returns all matching addresses.
        /// </summary>
        /// <param name="hexPattern">The byte pattern text, for example: <c>FF ?? ?? BA</c>.</param>
        /// <returns>All matching addresses within the main module range.</returns>
        public nint[] ScanAllMainModule(string hexPattern)
        {
            return ScanAllMainModule(hexPattern, DefaultScanChunkSize);
        }

        /// <summary>
        /// Scans the entire main module and returns all matching addresses.
        /// </summary>
        /// <param name="hexPattern">The byte pattern text, for example: <c>FF ?? ?? BA</c>.</param>
        /// <param name="chunkSize">The chunk size used to read process memory while scanning.</param>
        /// <returns>All matching addresses within the main module range.</returns>
        public nint[] ScanAllMainModule(string hexPattern, int chunkSize)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(hexPattern);
            Pattern<byte> pattern = BytePattern.Parse(hexPattern);
            return ScanAllMainModule(pattern, chunkSize);
        }

        /// <summary>
        /// Scans the entire main module and returns all matching addresses.
        /// </summary>
        /// <param name="pattern">The byte pattern to scan for.</param>
        /// <returns>All matching addresses within the main module range.</returns>
        public nint[] ScanAllMainModule(Pattern<byte> pattern)
        {
            return ScanAllMainModule(pattern, DefaultScanChunkSize);
        }

        /// <summary>
        /// Scans the entire main module and returns all matching addresses.
        /// </summary>
        /// <param name="pattern">The byte pattern to scan for.</param>
        /// <param name="chunkSize">The chunk size used to read process memory while scanning.</param>
        /// <returns>All matching addresses within the main module range.</returns>
        public nint[] ScanAllMainModule(Pattern<byte> pattern, int chunkSize)
        {
            ProcessModuleInfo mainModule = GetMainModule();
            return ScanAll(pattern, mainModule.BaseAddress, mainModule.EndAddress, chunkSize);
        }

        /// <summary>
        /// Scans a specific module for the first occurrence of a byte pattern.
        /// </summary>
        /// <param name="hexPattern">The byte pattern text, for example: <c>FF ?? ?? BA</c>.</param>
        /// <param name="moduleName">The module name or file name to scan.</param>
        /// <returns>The first matching address, or <see cref="IntPtr.Zero"/> when no match is found.</returns>
        public nint ScanModule(string hexPattern, string moduleName)
        {
            return ScanModule(hexPattern, moduleName, DefaultScanChunkSize);
        }

        /// <summary>
        /// Scans a specific module for the first occurrence of a byte pattern.
        /// </summary>
        /// <param name="hexPattern">The byte pattern text, for example: <c>FF ?? ?? BA</c>.</param>
        /// <param name="moduleName">The module name or file name to scan.</param>
        /// <param name="chunkSize">The chunk size used to read process memory while scanning.</param>
        /// <returns>The first matching address, or <see cref="IntPtr.Zero"/> when no match is found.</returns>
        public nint ScanModule(string hexPattern, string moduleName, int chunkSize)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(hexPattern);
            Pattern<byte> pattern = BytePattern.Parse(hexPattern);
            return ScanModule(pattern, moduleName, chunkSize);
        }

        /// <summary>
        /// Scans a specific module for the first occurrence of a byte pattern.
        /// </summary>
        /// <param name="pattern">The byte pattern to scan for.</param>
        /// <param name="moduleName">The module name or file name to scan.</param>
        /// <returns>The first matching address, or <see cref="IntPtr.Zero"/> when no match is found.</returns>
        public nint ScanModule(Pattern<byte> pattern, string moduleName)
        {
            return ScanModule(pattern, moduleName, DefaultScanChunkSize);
        }

        /// <summary>
        /// Scans a specific module for the first occurrence of a byte pattern.
        /// </summary>
        /// <param name="pattern">The byte pattern to scan for.</param>
        /// <param name="moduleName">The module name or file name to scan.</param>
        /// <param name="chunkSize">The chunk size used to read process memory while scanning.</param>
        /// <returns>The first matching address, or <see cref="IntPtr.Zero"/> when no match is found.</returns>
        public nint ScanModule(Pattern<byte> pattern, string moduleName, int chunkSize)
        {
            ProcessModuleInfo module = GetModule(moduleName);
            return Scan(pattern, module.BaseAddress, module.EndAddress, chunkSize);
        }

        /// <summary>
        /// Scans an entire module and returns all matching addresses.
        /// </summary>
        /// <param name="hexPattern">The byte pattern text, for example: <c>FF ?? ?? BA</c>.</param>
        /// <param name="moduleName">The module name or file name to scan.</param>
        /// <returns>All matching addresses within the module range.</returns>
        public nint[] ScanAllModule(string hexPattern, string moduleName)
        {
            return ScanAllModule(hexPattern, moduleName, DefaultScanChunkSize);
        }

        /// <summary>
        /// Scans an entire module and returns all matching addresses.
        /// </summary>
        /// <param name="hexPattern">The byte pattern text, for example: <c>FF ?? ?? BA</c>.</param>
        /// <param name="moduleName">The module name or file name to scan.</param>
        /// <param name="chunkSize">The chunk size used to read process memory while scanning.</param>
        /// <returns>All matching addresses within the module range.</returns>
        public nint[] ScanAllModule(string hexPattern, string moduleName, int chunkSize)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(hexPattern);
            Pattern<byte> pattern = BytePattern.Parse(hexPattern);
            return ScanAllModule(pattern, moduleName, chunkSize);
        }

        /// <summary>
        /// Scans an entire module and returns all matching addresses.
        /// </summary>
        /// <param name="pattern">The byte pattern to scan for.</param>
        /// <param name="moduleName">The module name or file name to scan.</param>
        /// <returns>All matching addresses within the module range.</returns>
        public nint[] ScanAllModule(Pattern<byte> pattern, string moduleName)
        {
            return ScanAllModule(pattern, moduleName, DefaultScanChunkSize);
        }

        /// <summary>
        /// Scans an entire module and returns all matching addresses.
        /// </summary>
        /// <param name="pattern">The byte pattern to scan for.</param>
        /// <param name="moduleName">The module name or file name to scan.</param>
        /// <param name="chunkSize">The chunk size used to read process memory while scanning.</param>
        /// <returns>All matching addresses within the module range.</returns>
        public nint[] ScanAllModule(Pattern<byte> pattern, string moduleName, int chunkSize)
        {
            ProcessModuleInfo module = GetModule(moduleName);
            return ScanAll(pattern, module.BaseAddress, module.EndAddress, chunkSize);
        }

        /// <summary>
        /// Scans an address range for the first occurrence of a byte pattern.
        /// </summary>
        /// <param name="hexPattern">The byte pattern text, for example: <c>FF ?? ?? BA</c>.</param>
        /// <param name="startAddress">The scan start address.</param>
        /// <param name="endAddress">The exclusive scan end address.</param>
        /// <returns>The first matching address, or <see cref="IntPtr.Zero"/> when no match is found.</returns>
        public nint Scan(string hexPattern, nint startAddress, nint endAddress)
        {
            return Scan(hexPattern, startAddress, endAddress, DefaultScanChunkSize);
        }

        /// <summary>
        /// Scans an address range for the first occurrence of a byte pattern.
        /// </summary>
        /// <param name="hexPattern">The byte pattern text, for example: <c>FF ?? ?? BA</c>.</param>
        /// <param name="startAddress">The scan start address.</param>
        /// <param name="endAddress">The exclusive scan end address.</param>
        /// <param name="chunkSize">The chunk size used to read process memory while scanning.</param>
        /// <returns>The first matching address, or <see cref="IntPtr.Zero"/> when no match is found.</returns>
        public nint Scan(string hexPattern, nint startAddress, nint endAddress, int chunkSize)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(hexPattern);
            Pattern<byte> pattern = BytePattern.Parse(hexPattern);
            return Scan(pattern, startAddress, endAddress, chunkSize);
        }

        /// <summary>
        /// Scans an address range for the first occurrence of a byte pattern.
        /// </summary>
        /// <param name="pattern">The byte pattern to scan for.</param>
        /// <param name="startAddress">The scan start address.</param>
        /// <param name="endAddress">The exclusive scan end address.</param>
        /// <returns>The first matching address, or <see cref="IntPtr.Zero"/> when no match is found.</returns>
        public nint Scan(Pattern<byte> pattern, nint startAddress, nint endAddress)
        {
            return Scan(pattern, startAddress, endAddress, DefaultScanChunkSize);
        }

        /// <summary>
        /// Scans an address range for the first occurrence of a byte pattern.
        /// </summary>
        /// <param name="pattern">The byte pattern to scan for.</param>
        /// <param name="startAddress">The scan start address.</param>
        /// <param name="endAddress">The exclusive scan end address.</param>
        /// <param name="chunkSize">The chunk size used to read process memory while scanning.</param>
        /// <returns>The first matching address, or <see cref="IntPtr.Zero"/> when no match is found.</returns>
        public nint Scan(Pattern<byte> pattern, nint startAddress, nint endAddress, int chunkSize)
        {
            return ScanFirst(pattern, startAddress, endAddress, chunkSize);
        }

        /// <summary>
        /// Scans an address range and returns all matching addresses.
        /// </summary>
        /// <param name="hexPattern">The byte pattern text, for example: <c>FF ?? ?? BA</c>.</param>
        /// <param name="startAddress">The scan start address.</param>
        /// <param name="endAddress">The exclusive scan end address.</param>
        /// <returns>All matching addresses within the provided range.</returns>
        public nint[] ScanAll(string hexPattern, nint startAddress, nint endAddress)
        {
            return ScanAll(hexPattern, startAddress, endAddress, DefaultScanChunkSize);
        }

        /// <summary>
        /// Scans an address range and returns all matching addresses.
        /// </summary>
        /// <param name="hexPattern">The byte pattern text, for example: <c>FF ?? ?? BA</c>.</param>
        /// <param name="startAddress">The scan start address.</param>
        /// <param name="endAddress">The exclusive scan end address.</param>
        /// <param name="chunkSize">The chunk size used to read process memory while scanning.</param>
        /// <returns>All matching addresses within the provided range.</returns>
        public nint[] ScanAll(string hexPattern, nint startAddress, nint endAddress, int chunkSize)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(hexPattern);
            Pattern<byte> pattern = BytePattern.Parse(hexPattern);
            return ScanAll(pattern, startAddress, endAddress, chunkSize);
        }

        /// <summary>
        /// Scans an address range and returns all matching addresses.
        /// </summary>
        /// <param name="pattern">The byte pattern to scan for.</param>
        /// <param name="startAddress">The scan start address.</param>
        /// <param name="endAddress">The exclusive scan end address.</param>
        /// <returns>All matching addresses within the provided range.</returns>
        public nint[] ScanAll(Pattern<byte> pattern, nint startAddress, nint endAddress)
        {
            return ScanAll(pattern, startAddress, endAddress, DefaultScanChunkSize);
        }

        /// <summary>
        /// Scans an address range and returns all matching addresses.
        /// </summary>
        /// <param name="pattern">The byte pattern to scan for.</param>
        /// <param name="startAddress">The scan start address.</param>
        /// <param name="endAddress">The exclusive scan end address.</param>
        /// <param name="chunkSize">The chunk size used to read process memory while scanning.</param>
        /// <returns>All matching addresses within the provided range.</returns>
        public nint[] ScanAll(Pattern<byte> pattern, nint startAddress, nint endAddress, int chunkSize)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ProcessMemoryValidation.ThrowIfInvalidAddress(startAddress);
            ProcessMemoryValidation.ThrowIfInvalidAddress(endAddress);
            if (endAddress <= startAddress)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(endAddress),
                    endAddress,
                    "End address must be greater than start address.");
            }

            if (chunkSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(chunkSize), chunkSize, "Chunk size must be greater than zero.");
            }

            long[] matches = BytePatternScanner.Scan(
                pattern,
                startAddress,
                endAddress,
                chunkSize,
                firstOnly: false,
                readChunk: (offset, destination) => ReadScanChunk((nint)offset, destination));

            nint[] result = new nint[matches.Length];

            for (int index = 0; index < matches.Length; index++)
            {
                result[index] = (nint)matches[index];
            }

            return result;
        }

        /// <summary>
        /// Releases process resources held by this reader.
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
        /// Creates a new <see cref="ProcessReader"/> for a single process that matches the provided name.
        /// </summary>
        /// <param name="processName">
        /// The process name to locate. This must resolve to exactly one running process.
        /// </param>
        /// <returns>A configured <see cref="ProcessReader"/> instance.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="processName"/> is null, empty, or whitespace.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when no process with the given name exists or when multiple matching processes are found.
        /// </exception>
        public static ProcessReader OpenByName(string processName) => new(processName);

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

        private int ReadScanChunk(nint address, Span<byte> destination)
        {
            _backend.Read(address, destination);
            return destination.Length;
        }

        private static int GetBytesUntilPageBoundary(nint address, int pageSize)
        {
            ulong pageSizeValue = (uint)pageSize;
            ulong absoluteAddress = unchecked((ulong)(long)address);
            int offsetInPage = (int)(absoluteAddress % pageSizeValue);
            int bytesUntilPageBoundary = pageSize - offsetInPage;
            if (bytesUntilPageBoundary <= 0)
            {
                return pageSize;
            }

            return bytesUntilPageBoundary;
        }
    }
}
