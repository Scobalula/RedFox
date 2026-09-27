namespace RedFox.Graphics3D.Avalonia;

using MediaBrush = global::Avalonia.Media.IBrush;

internal sealed class AnimationCurveListItem(int level, string text, SkeletonAnimationCurveComponent? component, MediaBrush? brush)
{
    public AnimationCurveListItem(int level, string text, SkeletonAnimationCurveComponent? component) : this(level, text, component, null) { }

    public int Level { get; } = level;

    public string Text { get; } = text;

    public SkeletonAnimationCurveComponent? Component { get; } = component;

    public MediaBrush? Brush { get; } = brush;
}
