namespace RedFox.Graphics3D.Preview.ViewModels;

public sealed class LoadedFileItem(string fullPath)
{
    public string FullPath { get; } = fullPath;

    public string DisplayName { get; } = Path.GetFileName(fullPath);
}
