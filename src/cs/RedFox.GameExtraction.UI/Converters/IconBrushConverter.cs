using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using RedFox.GameExtraction;

namespace RedFox.GameExtraction.UI.Converters;

/// <summary>
/// Resolves an asset category or settings group to its shared icon brush resource.
/// </summary>
public sealed class IconBrushConverter : IValueConverter
{
    /// <summary>
    /// Converts an asset category or settings group to its icon brush.
    /// </summary>
    /// <param name="value">The category value.</param>
    /// <param name="targetType">The binding target type.</param>
    /// <param name="parameter">An optional converter parameter.</param>
    /// <param name="culture">The binding culture.</param>
    /// <returns>The category icon brush, or the neutral icon brush when the category has no colour.</returns>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string resourceKey = value switch
        {
            AssetCategory.Model						=> "IconModelBrush",
            AssetCategory.Map						=> "IconModelBrush",
            AssetCategory.Animation					=> "IconAnimationBrush",
            AssetCategory.Cutscene					=> "IconAnimationBrush",
            AssetCategory.Image						=> "IconImageBrush",
            AssetCategory.Material					=> "IconImageBrush",
            AssetCategory.Shader					=> "IconImageBrush",
            AssetCategory.Effect					=> "IconImageBrush",
            AssetCategory.Sound						=> "IconSoundBrush",
            AssetCategory.Archive					=> "IconOpenBrush",
            GameExtractionSettingGroup.Model		=> "IconModelBrush",
            GameExtractionSettingGroup.Map			=> "IconModelBrush",
            GameExtractionSettingGroup.Animation	=> "IconAnimationBrush",
            GameExtractionSettingGroup.Cutscene		=> "IconAnimationBrush",
            GameExtractionSettingGroup.Image		=> "IconImageBrush",
            GameExtractionSettingGroup.Shader		=> "IconImageBrush",
            GameExtractionSettingGroup.Effect		=> "IconImageBrush",
            GameExtractionSettingGroup.Sound		=> "IconSoundBrush",
            GameExtractionSettingGroup.Archive		=> "IconOpenBrush",
            GameExtractionSettingGroup.Export		=> "IconExportBrush",
            GameExtractionSettingGroup.Preview		=> "AssetPreviewForegroundBrush",
            _										=> "ThemeMutedForegroundBrush",
        };

        return Application.Current?.Resources[resourceKey];
    }

    /// <summary>
    /// Converts the icon brush back to its enum value.
    /// </summary>
    /// <param name="value">The icon brush.</param>
    /// <param name="targetType">The binding target type.</param>
    /// <param name="parameter">An optional converter parameter.</param>
    /// <param name="culture">The binding culture.</param>
    /// <returns>This conversion is not supported.</returns>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
