using Avalonia.Controls;
using RedFox.GameExtraction;
using RedFox.GameExtraction.UI.ViewModels;

namespace RedFox.GameExtraction.UI.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    public void Initialize(SettingsBase settings, string appName)
    {
        var vm = new SettingsWindowViewModel(settings, appName);
        DataContext = vm;

        vm.SaveRequested += () => Close();
        vm.CancelRequested += () => Close();
    }
}
