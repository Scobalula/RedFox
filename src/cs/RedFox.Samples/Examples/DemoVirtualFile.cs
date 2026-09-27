using RedFox.IO.FileSystem;

namespace RedFox.Samples.Examples;

internal sealed class DemoVirtualFile(string name, byte[] bytes) : VirtualFile(name, bytes.Length)
{
    public override Stream Open() => new MemoryStream(bytes, writable: false);
}
