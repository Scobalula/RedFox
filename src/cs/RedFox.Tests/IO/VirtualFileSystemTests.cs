// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using RedFox.IO.FileSystem;

namespace RedFox.Tests.IO;

public sealed class VirtualFileSystemTests
{
    private sealed class MemoryVirtualFile(byte[] bytes) : VirtualFile(bytes.Length)
    {
        public override Stream Open() => new MemoryStream(bytes, writable: false);
    }

    [Fact]
    public void CreateDirectoriesAndAddFile_BuildsTreeAndFindsFile()
    {
        VirtualFileSystem fileSystem = new();
        MemoryVirtualFile file = new([1, 2, 3]);

        fileSystem.AddFile("root\\sub\\test.bin", file);

        Assert.True(fileSystem.TryGetFile("root\\sub\\test.bin", out VirtualFile? found));
        Assert.Same(file, found);
        Assert.Equal("root/sub/test.bin", found.FullPath);
        Assert.Equal(".bin", found.Extension);
    }

    [Fact]
    public void EnumerateFilesAndDirectories_RespectsSearchPatternAndDepth()
    {
        VirtualFileSystem fileSystem = new();
        fileSystem.AddFile("a\\alpha.txt", new MemoryVirtualFile([1]));
        fileSystem.AddFile("a\\b\\beta.dat", new MemoryVirtualFile([2]));
        fileSystem.AddFile("a\\b\\gamma.txt", new MemoryVirtualFile([3]));

        VirtualFile[] allTextFiles = fileSystem.Root.EnumerateFiles("*.txt", SearchOption.AllDirectories).ToArray();
        VirtualDirectory[] topDirectories = fileSystem.Root.EnumerateDirectories().ToArray();

        Assert.Equal(2, allTextFiles.Length);
        Assert.Single(topDirectories);
        Assert.Equal("a", topDirectories[0].Name);
    }

    [Fact]
    public void GetFile_WhenMissing_Throws()
    {
        VirtualFileSystem fileSystem = new();

        Assert.Throws<FileNotFoundException>(() => fileSystem.GetFile("missing.txt"));
    }

    [Fact]
    public void AddFile_DetectsDuplicateFileNames()
    {
        VirtualFileSystem fileSystem = new();
        fileSystem.AddFile("target\\same.txt", new MemoryVirtualFile([1]));

        Assert.Throws<IOException>(() => fileSystem.AddFile("target\\same.txt", new MemoryVirtualFile([2])));
    }

    [Fact]
    public void TryGetDirectory_And_TryGetFile_WorkCaseInsensitiveForDirectories()
    {
        VirtualFileSystem fileSystem = new();
        fileSystem.AddFile("Root\\Child\\Name.bin", new MemoryVirtualFile([9]));

        Assert.True(fileSystem.Root.TryGetDirectory("root\\child", out VirtualDirectory? directory));
        Assert.NotNull(directory);
        Assert.True(fileSystem.TryGetFile("root\\child\\Name.bin", out VirtualFile? file));
        Assert.NotNull(file);
    }
}
