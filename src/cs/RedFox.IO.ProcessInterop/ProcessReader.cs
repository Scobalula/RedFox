using System;
using System.Buffers.Binary;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.ProcessStatus;

namespace RedFox.IO.ProcessInterop
{
    [SupportedOSPlatform("windows7.0")]
    public class ProcessReader : IDisposable
    {
        internal HANDLE Handle { get; set; }

        public ProcessReader(string processName)
        {
            var processes = Process.GetProcessesByName(processName);

            if (processes.Length == 0)
                throw new IOException($"");

        }

        public ProcessReader(Process process) : this(process.Id) { }

        public ProcessReader(int processID)
        {
            Handle = PInvoke.OpenProcess(Windows.Win32.System.Threading.PROCESS_ACCESS_RIGHTS.PROCESS_ALL_ACCESS, false, (uint)processID);

            if (Handle == HANDLE.Null)
                throw new Win32Exception();
        }

        public ushort ReadUInt16(long address)
        {
            Span<byte> buf = stackalloc byte[Unsafe.SizeOf<ushort>()];
            ReadExactly(buf, address);
            return BinaryPrimitives.ReadUInt16LittleEndian(buf);
        }
        public short ReadInt16(long address)
        {
            Span<byte> buf = stackalloc byte[Unsafe.SizeOf<short>()];
            ReadExactly(buf, address);
            return BinaryPrimitives.ReadInt16LittleEndian(buf);
        }
        public uint ReadUInt32(long address)
        {
            Span<byte> buf = stackalloc byte[Unsafe.SizeOf<uint>()];
            ReadExactly(buf, address);
            return BinaryPrimitives.ReadUInt32LittleEndian(buf);
        }
        public int ReadInt32(long address)
        {
            Span<byte> buf = stackalloc byte[Unsafe.SizeOf<int>()];
            ReadExactly(buf, address);
            return BinaryPrimitives.ReadInt32LittleEndian(buf);
        }
        public ulong ReadUInt64(long address)
        {
            Span<byte> buf = stackalloc byte[Unsafe.SizeOf<ulong>()];
            ReadExactly(buf, address);
            return BinaryPrimitives.ReadUInt64LittleEndian(buf);
        }
        public long ReadInt64(long address)
        {
            Span<byte> buf = stackalloc byte[Unsafe.SizeOf<long>()];
            ReadExactly(buf, address);
            return BinaryPrimitives.ReadInt64LittleEndian(buf);
        }
        public float ReadSingle(long address)
        {
            Span<byte> buf = stackalloc byte[Unsafe.SizeOf<float>()];
            ReadExactly(buf, address);
            return BinaryPrimitives.ReadSingleLittleEndian(buf);
        }
        public double ReadDouble(long address)
        {
            Span<byte> buf = stackalloc byte[Unsafe.SizeOf<double>()];
            ReadExactly(buf, address);
            return BinaryPrimitives.ReadDoubleLittleEndian(buf);
        }

        public unsafe void ReadExactly(Span<byte> buffer, long address)
        {
            fixed(byte* p = buffer)
            {
                if (!PInvoke.ReadProcessMemory(Handle, (void*)address, p, (nuint)buffer.Length))
                    throw new Win32Exception();
            }
        }

        public byte[] Read(int size, long address)
        {
            var buf = new byte[size];
            ReadExactly(buf, address);
            return buf;
        }

        public int Read(byte[] buffer, int offset, int size, long address) => Read(buffer.AsSpan()[offset..size], address);

        public unsafe int Read(Span<byte> buffer, long address)
        {
            fixed (byte* p = buffer)
            {
                nuint v = 0;

                if (!PInvoke.ReadProcessMemory(Handle, (void*)address, p, (nuint)buffer.Length, &v))
                    return 0;

                return (int)v;
            }
        }

        public IEnumerable<long> FindBytes(string pattern)
        {
            var b = BytePattern.Parse(pattern);
            var buffer = new byte[65535];
            var address = GetModuleAddress();
            var addressEnd = address + GetModuleSize();

            int needleIndex = 0;

            while (true)
            {
                var bytesToRead = (int)Math.Min(addressEnd - address, buffer.Length);
                var bytesRead = Read(buffer, 0, bytesToRead, address);

                if (bytesRead == 0)
                    break;

                for (int i = 0; i < bytesRead; i++)
                {
                    if (b.Needle[needleIndex] == buffer[i] || b.Mask[needleIndex] == 0xFF)
                    {
                        needleIndex++;

                        if (needleIndex == b.Needle.Length)
                        {
                            yield return address + i + 1 - b.Needle.Length;

                            needleIndex = 0;
                        }
                    }
                    else
                    {
                        needleIndex = 0;

                        if (b.Needle[needleIndex] == buffer[i] || b.Needle[needleIndex] == 0xFF)
                        {
                            needleIndex++;

                            if (needleIndex == b.Needle.Length)
                            {
                                yield return address + i + 1 - b.Needle.Length;

                                needleIndex = 0;
                            }
                        }
                    }
                }

                if (bytesRead < buffer.Length)
                    break;

                address += bytesRead;
            }
        }

        public unsafe long GetModuleAddress()
        {
            // 3. Prepare an array to hold module handles
            Span<HMODULE> moduleHandles = stackalloc HMODULE[1]; // Assuming a maximum of 1024 modules
            uint cbNeeded;

            fixed (HMODULE* mods = moduleHandles)
            {
                // 4. Enumerate modules
                bool success = PInvoke.EnumProcessModulesEx(Handle, mods, (uint)moduleHandles.Length * (uint)IntPtr.Size, &cbNeeded, ENUM_PROCESS_MODULES_EX_FLAGS.LIST_MODULES_ALL);

                int numModules = (int)(cbNeeded / Unsafe.SizeOf<HMODULE>());

                var moduleInfo = new MODULEINFO();
                success = PInvoke.GetModuleInformation(Handle, moduleHandles[0], &moduleInfo, (uint)Unsafe.SizeOf<MODULEINFO>());

                return (long)moduleInfo.lpBaseOfDll;
            }
        }

        public unsafe int GetModuleSize()
        {
            // 3. Prepare an array to hold module handles
            Span<HMODULE> moduleHandles = stackalloc HMODULE[1]; // Assuming a maximum of 1024 modules
            uint cbNeeded;

            fixed (HMODULE* mods = moduleHandles)
            {
                // 4. Enumerate modules
                bool success = PInvoke.EnumProcessModulesEx(Handle, mods, (uint)moduleHandles.Length * (uint)IntPtr.Size, &cbNeeded, ENUM_PROCESS_MODULES_EX_FLAGS.LIST_MODULES_ALL);

                int numModules = (int)(cbNeeded / Unsafe.SizeOf<HMODULE>());

                var moduleInfo = new MODULEINFO();
                success = PInvoke.GetModuleInformation(Handle, moduleHandles[0], &moduleInfo, (uint)Unsafe.SizeOf<MODULEINFO>());

                return (int)moduleInfo.SizeOfImage;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            if (Handle != IntPtr.Zero)
            {
                PInvoke.CloseHandle(Handle);
            }

            GC.SuppressFinalize(this);
        }
    }
}
