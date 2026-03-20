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
    private sealed class MemoryVirtualFile(string name, byte[] bytes) : VirtualFile(name, bytes.Length)
    {
        public override Stream Open() => new MemoryStream(bytes, writable: false);
    }

    [Fact]
    public void CreateDirectoriesAndAddFile_BuildsTreeAndFindsFile()
    {
        VirtualFileSystem fileSystem = new();
        MemoryVirtualFile file = new("test.bin", [1, 2, 3]);

        fileSystem.AddFile("root\\sub\\test.bin", file);

        Assert.True(fileSystem.TryGetFile("root\\sub\\test.bin", out VirtualFile? found));
        Assert.Same(file, found);
        Assert.Equal("root\\sub\\test.bin", found.FullPath);
        Assert.Equal(".bin", found.Extension);
    }

    [Fact]
    public void EnumerateFilesAndDirectories_RespectsSearchPatternAndDepth()
    {
        VirtualFileSystem fileSystem = new();
        fileSystem.AddFile("a\\alpha.txt", new MemoryVirtualFile("alpha.txt", [1]));
        fileSystem.AddFile("a\\b\\beta.dat", new MemoryVirtualFile("beta.dat", [2]));
        fileSystem.AddFile("a\\b\\gamma.txt", new MemoryVirtualFile("gamma.txt", [3]));

        VirtualFile[] allTextFiles = fileSystem.EnumerateFiles(null, "*.txt", SearchOption.AllDirectories).ToArray();
        VirtualDirectory[] topDirectories = fileSystem.Root.EnumerateDirectories(null, "*", SearchOption.TopDirectoryOnly).ToArray();

        Assert.Equal(2, allTextFiles.Length);
        Assert.Single(topDirectories);
        Assert.Equal("a", topDirectories[0].Name);
    }

    [Fact]
    public void GetFile_WhenMissing_Throws()
    {
        VirtualDirectory directory = new("root");

        Assert.Throws<FileNotFoundException>(() => directory.GetFile("missing.txt"));
    }

    [Fact]
    public void MoveTo_DetectsDuplicateFileNames()
    {
        VirtualDirectory root = new("root");
        VirtualDirectory target = root.CreateDirectory("target");
        MemoryVirtualFile first = new("same.txt", [1]);
        MemoryVirtualFile second = new("same.txt", [2]);
        first.MoveTo(target);

        Assert.Throws<IOException>(() => second.MoveTo(target));
    }

    [Fact]
    public void MoveTo_DetectsDuplicateDirectoryNames()
    {
        VirtualDirectory root = new("root");
        VirtualDirectory parent = root.CreateDirectory("p");
        VirtualDirectory first = new("child");
        VirtualDirectory second = new("child");
        first.MoveTo(parent);

        Assert.Throws<IOException>(() => second.MoveTo(parent));
    }

    [Fact]
    public void ValidityHelpers_ReturnExpectedResults()
    {
        Assert.True(VirtualFileSystem.IsValidFileName("hello.txt"));
        Assert.True(VirtualFileSystem.IsValidDirectoryName("folder"));
        Assert.True(VirtualFileSystem.IsValidFullPath("one\\two\\file.bin"));
        Assert.False(VirtualFileSystem.IsValidFileName(" "));
        Assert.False(VirtualFileSystem.IsValidFullPath(""));
    }

    [Fact]
    public void TryGetDirectory_And_TryGetFile_WorkCaseInsensitiveForDirectories()
    {
        VirtualFileSystem fileSystem = new();
        fileSystem.AddFile("Root\\Child\\Name.bin", new MemoryVirtualFile("Name.bin", [9]));

        Assert.True(fileSystem.Root.TryGetDirectory("root\\child", out VirtualDirectory? directory));
        Assert.NotNull(directory);
        Assert.True(fileSystem.TryGetFile("root\\child\\Name.bin", out VirtualFile? file));
        Assert.NotNull(file);
    }
}
