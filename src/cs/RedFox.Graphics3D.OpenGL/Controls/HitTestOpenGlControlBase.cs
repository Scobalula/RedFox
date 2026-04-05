using Avalonia;
using Avalonia.OpenGL.Controls;
using Avalonia.Rendering;

namespace RedFox.Graphics3D.OpenGL.Controls;

/// <summary>
/// Adds bounds-based hit testing for OpenGL composition-surface controls.
/// </summary>
public abstract class HitTestOpenGlControlBase : OpenGlControlBase, ICustomHitTest
{
    public bool HitTest(Point point) => Bounds.Contains(point);
}
