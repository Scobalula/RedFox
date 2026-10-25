using CommunityToolkit.Mvvm.ComponentModel;

namespace RedFox.GameExtraction.UI.ViewModels;

public partial class SettingOptionViewModel : ObservableObject
{
    public string Name { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public SettingOptionViewModel(string name, bool isSelected)
    {
        Name		= name;
        IsSelected	= isSelected;
    }
}
