using Avalonia.Controls;
using RedFox.GameExtraction.UI.ViewModels;

namespace RedFox.GameExtraction.UI.Views;

internal sealed class PreviewWindowHost : IDisposable
{
    private PreviewWindow? _window;

    public event Action? Closed;

    public void Show(Window owner, MainWindowViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(viewModel);

        PreviewWindow window = GetOrCreateWindow(viewModel);
        if (!window.IsVisible)
        {
            window.Show(owner);
        }

        window.Activate();
    }

    public void Dispose()
    {
        if (_window is null)
        {
            return;
        }

        PreviewWindow window = _window;
        _window = null;
        window.Closed -= OnWindowClosed;

        if (window.IsVisible)
        {
            window.Close();
        }
    }

    private PreviewWindow GetOrCreateWindow(MainWindowViewModel viewModel)
    {
        if (_window is not null)
        {
            if (!ReferenceEquals(_window.DataContext, viewModel))
            {
                _window.DataContext = viewModel;
            }

            return _window;
        }

        _window = new PreviewWindow
        {
            DataContext = viewModel,
        };
        _window.Closed += OnWindowClosed;
        return _window;
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (_window is null)
        {
            return;
        }

        _window.Closed -= OnWindowClosed;
        _window = null;
        Closed?.Invoke();
    }
}