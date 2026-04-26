using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using System.Globalization;

namespace RedFox.Samples.Examples;

internal sealed class AvaloniaSampleApp : Application
{
    private System.Timers.Timer? _exitTimer;

    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            string[] arguments = desktop.Args ?? [];
            AvaloniaSampleWindow window = new(arguments);
            desktop.MainWindow = window;
            double exitAfterSeconds = ParseExitAfterSeconds(arguments);
            if (exitAfterSeconds > 0.0)
            {
                _exitTimer = new System.Timers.Timer(exitAfterSeconds * 1000.0)
                {
                    AutoReset = false
                };
                _exitTimer.Elapsed += (_, _) => global::Avalonia.Threading.Dispatcher.UIThread.Post(window.Close);
                _exitTimer.Start();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static double ParseExitAfterSeconds(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (!arg.StartsWith("--exit-after=", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string value = arg[13..];
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds) && seconds > 0.0)
            {
                return seconds;
            }
        }

        return 0.0;
    }
}
