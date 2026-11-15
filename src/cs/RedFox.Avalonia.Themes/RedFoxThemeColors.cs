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
    /// Mixes an accent into a surface color, normalizing the accent's saturation and brightness so different accents tint by a similar visible amount.
    /// </summary>
    /// <param name="surface">The surface color to tint.</param>
    /// <param name="accent">The accent color.</param>
    /// <param name="amount">The tint strength, from 0 (none) to 1.</param>
    /// <returns>The opaque tinted color.</returns>
    public static Color Tint(Color surface, Color accent, double amount)
    {
        HslColor hsl		= accent.ToHsl();
        Color normalized	= HslColor.ToRgb(hsl.H, Math.Min(hsl.S, 0.7), 0.45);
        double luma			= (normalized.R * 0.299 + normalized.G * 0.587 + normalized.B * 0.114) / byte.MaxValue;
        double weight		= Math.Clamp(amount * 0.35 / luma, 0.0, 1.0);

        return Color.FromRgb((byte)(surface.R + (normalized.R - surface.R) * weight), (byte)(surface.G + (normalized.G - surface.G) * weight), (byte)(surface.B + (normalized.B - surface.B) * weight));
    }

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