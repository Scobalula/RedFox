using System.ComponentModel;
using System.Runtime.InteropServices;

namespace RedFox.AppHost.Packer;

/// <summary>
/// Copies the Win32 resources and subsystem of the SDK apphost onto the launcher.
/// </summary>
internal static unsafe partial class ExecutableResources
{
    private const uint LoadLibraryAsDatafile		= 0x02;
    private const uint LoadLibraryAsImageResource	= 0x20;

    private static readonly nint[] CopiedTypes = [3, 14, 16, 24];

    /// <summary>
    /// Copies the icon, version and manifest resources from the source executable to the target executable.
    /// </summary>
    /// <param name="sourcePath">The executable to copy resources from.</param>
    /// <param name="targetPath">The executable to copy resources to.</param>
    public static void Copy(string sourcePath, string targetPath)
    {
        nint module = LoadLibraryExW(sourcePath, 0, LoadLibraryAsDatafile | LoadLibraryAsImageResource);

        if (module == 0)
        {
            throw new Win32Exception();
        }

        nint update = BeginUpdateResourceW(targetPath, false);

        if (update == 0)
        {
            throw new Win32Exception();
        }

        foreach (nint type in CopiedTypes)
        {
            foreach (nint name in Enumerate(handle => EnumResourceNamesW(module, type, &OnName, handle)))
            {
                foreach (nint language in Enumerate(handle => EnumResourceLanguagesW(module, type, name, &OnLanguage, handle)))
                {
                    nint resource	= FindResourceExW(module, type, name, (ushort)language);
                    nint data		= LockResource(LoadResource(module, resource));

                    if (!UpdateResourceW(update, type, name, (ushort)language, data, SizeofResource(module, resource)))
                    {
                        throw new Win32Exception();
                    }
                }

                if (name > ushort.MaxValue)
                {
                    Marshal.FreeHGlobal(name);
                }
            }
        }

        if (!EndUpdateResourceW(update, false))
        {
            throw new Win32Exception();
        }

        FreeLibrary(module);
    }

    /// <summary>
    /// Copies the PE subsystem (GUI or console) from the source executable to the target executable.
    /// </summary>
    /// <param name="sourcePath">The executable to copy the subsystem from.</param>
    /// <param name="targetPath">The executable to copy the subsystem to.</param>
    /// <returns><see langword="true"/> if the subsystem is console; otherwise <see langword="false"/>.</returns>
    public static bool CopySubsystem(string sourcePath, string targetPath)
    {
        using var source = new BinaryReader(File.OpenRead(sourcePath));
        using var target = new BinaryWriter(File.Open(targetPath, FileMode.Open, FileAccess.ReadWrite));

        source.BaseStream.Position = GetSubsystemOffset(source.BaseStream);
        target.BaseStream.Position = GetSubsystemOffset(target.BaseStream);

        ushort subsystem = source.ReadUInt16();
        target.Write(subsystem);
        return subsystem == 3;
    }

    private static long GetSubsystemOffset(Stream stream)
    {
        Span<byte> offset = stackalloc byte[sizeof(int)];

        stream.Position = 0x3C;
        stream.ReadExactly(offset);
        return BitConverter.ToInt32(offset) + 0x5C;
    }

    private static List<nint> Enumerate(Func<nint, bool> enumerate)
    {
        var values = new List<nint>();
        var handle = GCHandle.Alloc(values);

        enumerate(GCHandle.ToIntPtr(handle));
        handle.Free();
        return values;
    }

    [UnmanagedCallersOnly]
    private static int OnName(nint module, nint type, nint name, nint parameter)
    {
        if (GCHandle.FromIntPtr(parameter).Target is List<nint> names)
        {
            names.Add(name > ushort.MaxValue ? Marshal.StringToHGlobalUni(new string((char*)name)) : name);
        }

        return 1;
    }

    [UnmanagedCallersOnly]
    private static int OnLanguage(nint module, nint type, nint name, ushort language, nint parameter)
    {
        if (GCHandle.FromIntPtr(parameter).Target is List<nint> languages)
        {
            languages.Add(language);
        }

        return 1;
    }

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint LoadLibraryExW(string fileName, nint file, uint flags);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool FreeLibrary(nint module);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EnumResourceNamesW(nint module, nint type, delegate* unmanaged<nint, nint, nint, nint, int> callback, nint parameter);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EnumResourceLanguagesW(nint module, nint type, nint name, delegate* unmanaged<nint, nint, nint, ushort, nint, int> callback, nint parameter);

    [LibraryImport("kernel32.dll")]
    private static partial nint FindResourceExW(nint module, nint type, nint name, ushort language);

    [LibraryImport("kernel32.dll")]
    private static partial nint LoadResource(nint module, nint resource);

    [LibraryImport("kernel32.dll")]
    private static partial nint LockResource(nint data);

    [LibraryImport("kernel32.dll")]
    private static partial uint SizeofResource(nint module, nint resource);

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint BeginUpdateResourceW(string fileName, [MarshalAs(UnmanagedType.Bool)] bool deleteExistingResources);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UpdateResourceW(nint update, nint type, nint name, ushort language, nint data, uint size);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EndUpdateResourceW(nint update, [MarshalAs(UnmanagedType.Bool)] bool discard);
}
