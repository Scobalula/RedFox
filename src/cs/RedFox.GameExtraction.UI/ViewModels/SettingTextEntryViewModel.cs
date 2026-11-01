using CommunityToolkit.Mvvm.ComponentModel;

namespace RedFox.GameExtraction.UI.ViewModels;

/// <summary>
/// Provides an editable entry of a text array setting.
/// </summary>
public partial class SettingTextEntryViewModel : ObservableObject
{
    /// <summary>
    /// Gets or sets the entry value.
    /// </summary>
    [ObservableProperty]
    public partial string Value { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingTextEntryViewModel"/> class.
    /// </summary>
    /// <param name="value">The entry value.</param>
    public SettingTextEntryViewModel(string value)
    {
        Value = value;
    }
}
