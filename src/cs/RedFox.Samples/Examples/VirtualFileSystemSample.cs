// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using RedFox.IO.FileSystem;

namespace RedFox.Samples.Examples;

/// <summary>
/// Demonstrates basic virtual file system operations.
/// </summary>
internal sealed class VirtualFileSystemSample : ISample
{
    private sealed class DemoFile(string name, byte[] bytes) : VirtualFile(name, bytes.Length)
    {
        public override Stream Open() => new MemoryStream(bytes, writable: false);
    }

    /// <inheritdoc />
    public string Name => "io-vfs";

    /// <inheritdoc />
    public string Description => "Creates a virtual directory tree, adds files, and enumerates results.";

    /// <inheritdoc />
    public int Run(string[] arguments)
    {
        VirtualFileSystem virtualFileSystem = new();
        virtualFileSystem.AddFile("assets\\textures\\fox.dds", new DemoFile("fox.dds", [0x44, 0x44, 0x53]));
        virtualFileSystem.AddFile("assets\\scripts\\init.lua", new DemoFile("init.lua", [0x2D, 0x2D]));

        Console.WriteLine("All files:");
        foreach (VirtualFile file in virtualFileSystem.EnumerateFiles(null, "*", SearchOption.AllDirectories))
        {
            Console.WriteLine($"  {file.FullPath} ({file.Size} bytes)");
        }

        Console.WriteLine("Directories:");
        foreach (VirtualDirectory directory in virtualFileSystem.EnumerateDirectories(null, "*", SearchOption.AllDirectories))
        {
            Console.WriteLine($"  {directory.FullPath}");
        }

        return 0;
    }
}
