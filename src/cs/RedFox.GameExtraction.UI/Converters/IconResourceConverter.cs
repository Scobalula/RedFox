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
            AssetCategory.Model						=> "IconModel",
            AssetCategory.Animation					=> "IconAnimation",
            AssetCategory.Image						=> "IconImage",
            AssetCategory.Material					=> "IconMaterial",
            AssetCategory.Sound						=> "IconSound",
            AssetCategory.Archive					=> "IconArchive",
            AssetCategory.Document					=> "IconDocument",
            AssetCategory.Other						=> "IconOther",
            AssetCategory.Script					=> "IconScript",
            AssetCategory.Table						=> "IconTable",
            AssetCategory.Binary					=> "IconBinary",
            AssetCategory.Map						=> "IconMap",
            AssetCategory.Shader					=> "IconShader",
            AssetCategory.Effect					=> "IconEffect",
            AssetCategory.Cutscene					=> "IconCutscene",
            GameExtractionSettingGroup.Export		=> "IconExport",
            GameExtractionSettingGroup.Model		=> "IconModel",
            GameExtractionSettingGroup.Animation	=> "IconAnimation",
            GameExtractionSettingGroup.Image		=> "IconImage",
            GameExtractionSettingGroup.Sound		=> "IconSound",
            GameExtractionSettingGroup.Archive		=> "IconArchive",
            GameExtractionSettingGroup.Document		=> "IconDocument",
            GameExtractionSettingGroup.General		=> "IconSettings",
            GameExtractionSettingGroup.Preview		=> "IconEye",
            GameExtractionSettingGroup.Other		=> "IconOther",
            GameExtractionSettingGroup.Script		=> "IconScript",
            GameExtractionSettingGroup.Table		=> "IconTable",
            GameExtractionSettingGroup.Binary		=> "IconBinary",
            GameExtractionSettingGroup.Map			=> "IconMap",
            GameExtractionSettingGroup.Shader		=> "IconShader",
            GameExtractionSettingGroup.Effect		=> "IconEffect",
            GameExtractionSettingGroup.Cutscene		=> "IconCutscene",
            _										=> "IconFile",
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
