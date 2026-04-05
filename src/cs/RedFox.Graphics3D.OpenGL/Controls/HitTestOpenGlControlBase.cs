using Avalonia;
using Avalonia.OpenGL.Controls;
using Avalonia.Rendering;

namespace RedFox.Graphics3D.OpenGL.Controls;

/// <summary>
/// Adds bounds-based hit testing for OpenGL composition-surface controls.
/// </summary>
public abstract class HitTestOpenGlControlBase : OpenGlControlBase, ICustomHitTest
{
    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="point"/> lies inside the control's bounds.
    /// </summary>
    /// <param name="point">The point to test, in control coordinates.</param>
    public bool HitTest(Point point) => Bounds.Contains(point);
}
