namespace RedFox.Graphics3D.Avalonia;

using MediaBrush = global::Avalonia.Media.IBrush;

internal sealed class AnimationCurveListItem
{
    public AnimationCurveListItem(int level, string text, SkeletonAnimationCurveComponent? component, MediaBrush? brush = null)
    {
        Level = level;
        Text = text;
        Component = component;
        Brush = brush;
    }

    public int Level { get; }

    public string Text { get; }

    public SkeletonAnimationCurveComponent? Component { get; }

    public MediaBrush? Brush { get; }
}
