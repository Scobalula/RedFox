namespace RedFox.Graphics3D.Avalonia;

internal sealed class AnimationCurveListItem
{
    public AnimationCurveListItem(int level, string text, SkeletonAnimationCurveComponent? component)
    {
        Level = level;
        Text = text;
        Component = component;
    }

    public int Level { get; }

    public string Text { get; }

    public SkeletonAnimationCurveComponent? Component { get; }
}