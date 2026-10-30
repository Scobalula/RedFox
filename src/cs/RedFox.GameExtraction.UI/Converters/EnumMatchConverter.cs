using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace RedFox.GameExtraction.UI.Converters;

/// <summary>
/// Checks whether an enum value matches the converter parameter, for binding radio items to an enum property.
/// </summary>
public sealed class EnumMatchConverter : IValueConverter
{
    /// <summary>
    /// Returns whether the bound value equals the converter parameter.
    /// </summary>
    /// <param name="value">The bound enum value.</param>
    /// <param name="targetType">The binding target type.</param>
    /// <param name="parameter">The enum value represented by the target.</param>
    /// <param name="culture">The binding culture.</param>
    /// <returns><see langword="true"/> when the value matches the parameter.</returns>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => Equals(value, parameter);

    /// <summary>
    /// Returns the converter parameter when the target becomes checked.
    /// </summary>
    /// <param name="value">The checked state.</param>
    /// <param name="targetType">The binding source type.</param>
    /// <param name="parameter">The enum value represented by the target.</param>
    /// <param name="culture">The binding culture.</param>
    /// <returns>The parameter when checked, otherwise <see cref="BindingOperations.DoNothing"/>.</returns>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is true ? parameter : BindingOperations.DoNothing;
}
