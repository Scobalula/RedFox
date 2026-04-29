using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using RedFox.Graphics3D.Avalonia;

namespace RedFox.Samples.Examples;

internal sealed partial class AvaloniaAnimationCurvesWindow : Window
{
    private readonly AvaloniaAnimationCurvesViewModel _viewModel;

    public AvaloniaAnimationCurvesWindow(string[] arguments)
    {
        InitializeComponent();
        _viewModel = new AvaloniaAnimationCurvesViewModel(arguments);
        DataContext = _viewModel;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private async void OnOpenClick(object? sender, RoutedEventArgs e)
    {
        await _viewModel.OpenFilesAsync(StorageProvider);
    }

    private void OnRendererFrame(object? sender, AvaloniaRenderFrameEventArgs e)
    {
        _viewModel.OnRenderFrame(e);
    }
}