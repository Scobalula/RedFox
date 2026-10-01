using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using RedFox.GameExtraction;

namespace RedFox.GameExtraction.UI.Converters;

/// <summary>
/// Resolves an asset category or settings group to its shared icon geometry resource.
/// </summary>
public sealed class IconResourceConverter : IValueConverter
{
    /// <summary>
    /// Converts an asset category or settings group to its icon geometry.
    /// </summary>
    /// <param name="value">The category value.</param>
    /// <param name="targetType">The binding target type.</param>
    /// <param name="parameter">An optional converter parameter.</param>
    /// <param name="culture">The binding culture.</param>
    /// <returns>The category icon geometry, or the generic file icon when unavailable.</returns>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string resourceKey = value switch
        {
            AssetCategory.Model => "IconModel",
            AssetCategory.Animation => "IconPlay",
            AssetCategory.Image => "IconImage",
            AssetCategory.Sound => "IconSound",
            AssetCategory.Archive => "IconArchive",
            AssetCategory.Document => "IconFile",
            AssetCategory.Other => "IconOther",
            GameExtractionSettingGroup.Export => "IconExport",
            GameExtractionSettingGroup.Model => "IconModel",
            GameExtractionSettingGroup.Animation => "IconPlay",
            GameExtractionSettingGroup.Image => "IconImage",
            GameExtractionSettingGroup.Sound => "IconSound",
            GameExtractionSettingGroup.Archive => "IconArchive",
            GameExtractionSettingGroup.Document => "IconFile",
            GameExtractionSettingGroup.General => "IconSettings",
            GameExtractionSettingGroup.Other => "IconOther",
            _ => "IconFile",
        };

        return Application.Current?.Resources[resourceKey];
    }

    /// <summary>
    /// Converts the icon geometry back to its enum value.
    /// </summary>
    /// <param name="value">The icon geometry.</param>
    /// <param name="targetType">The binding target type.</param>
    /// <param name="parameter">An optional converter parameter.</param>
    /// <param name="culture">The binding culture.</param>
    /// <returns>This conversion is not supported.</returns>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
