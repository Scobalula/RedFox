namespace RedFox.Graphics3D.Preview.ViewModels;

public sealed class AnimationOptionItem(string displayName, string? value)
{
    public string DisplayName { get; } = displayName;

    public string? Value { get; } = value;
}
