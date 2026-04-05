using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace RedFox.Graphics3D.Preview;

public partial class App : Application
{
    public static PreviewCliOptions LaunchOptions { get; set; } = new();

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow(LaunchOptions);

        base.OnFrameworkInitializationCompleted();
    }
}
