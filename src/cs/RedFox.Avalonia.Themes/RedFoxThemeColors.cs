using System.Numerics;
using Avalonia.Media;

namespace RedFox.Avalonia.Themes;

/// <summary>
/// Provides shared RedFox theme colors for Avalonia UI and renderer hosts.
/// </summary>
public static class RedFoxThemeColors
{
    /// <summary>
    /// Gets the base application background color.
    /// </summary>
    public static Color Background { get; } = Color.FromRgb(22, 22, 24);

    /// <summary>
    /// Gets the elevated surface color.
    /// </summary>
    public static Color Surface { get; } = Color.FromRgb(30, 30, 33);

    /// <summary>
    /// Gets the graph and viewport surface color.
    /// </summary>
    public static Color PlotSurface { get; } = Color.FromRgb(33, 33, 36);

    /// <summary>
    /// Gets the subtle border and splitter color.
    /// </summary>
    public static Color Border { get; } = Color.FromRgb(42, 42, 46);

    /// <summary>
    /// Gets the primary accent color.
    /// </summary>
    public static Color Accent { get; } = Color.FromRgb(11, 125, 146);

    /// <summary>
    /// Gets the curve accent color.
    /// </summary>
    public static Color Curve { get; } = Color.FromRgb(255, 167, 38);

    /// <summary>
    /// Gets the application text color.
    /// </summary>
    public static Color Foreground { get; } = Color.FromRgb(228, 228, 234);

    /// <summary>
    /// Gets the muted text color.
    /// </summary>
    public static Color MutedForeground { get; } = Color.FromRgb(120, 120, 126);

    /// <summary>
    /// Gets the renderer clear color as a normalized vector.
    /// </summary>
    public static Vector4 SceneBackgroundVector => ToVector4(Background);

    /// <summary>
    /// Converts an Avalonia color to a normalized renderer color vector.
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>The normalized color vector.</returns>
    public static Vector4 ToVector4(Color color)
    {
        const float scale = 1.0f / byte.MaxValue;
        return new Vector4(color.R * scale, color.G * scale, color.B * scale, color.A * scale);
    }
}