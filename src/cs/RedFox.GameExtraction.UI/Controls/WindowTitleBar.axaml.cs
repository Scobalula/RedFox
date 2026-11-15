using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace RedFox.GameExtraction.UI.Controls;

/// <summary>
/// Draws the window title bar with an optional icon, a title and caption buttons. The host window needs the custom-chrome class.
/// </summary>
public partial class WindowTitleBar : UserControl
{
    /// <summary>
    /// Defines the <see cref="Icon"/> property.
    /// </summary>
    public static readonly StyledProperty<IImage?> IconProperty = AvaloniaProperty.Register<WindowTitleBar, IImage?>(nameof(Icon));

    /// <summary>
    /// Defines the <see cref="Title"/> property.
    /// </summary>
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<WindowTitleBar, string?>(nameof(Title));

    /// <summary>
    /// Defines the <see cref="IsMaximized"/> property.
    /// </summary>
    public static readonly DirectProperty<WindowTitleBar, bool> IsMaximizedProperty = AvaloniaProperty.RegisterDirect<WindowTitleBar, bool>(nameof(IsMaximized), titleBar => titleBar.IsMaximized);

    private Window? _window;
    private bool _isMaximized;

    /// <summary>
    /// Gets or sets the application icon shown before the title.
    /// </summary>
    public IImage? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>
    /// Gets or sets the title text. Uses the host window title when not set.
    /// </summary>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>
    /// Gets whether the host window is maximised.
    /// </summary>
    public bool IsMaximized
    {
        get => _isMaximized;
        private set
        {
            if (SetAndRaise(IsMaximizedProperty, ref _isMaximized, value))
            {
                Classes.Set("maximized", value);
            }
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowTitleBar"/> class.
    /// </summary>
    public WindowTitleBar()
    {
        InitializeComponent();
    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _window = TopLevel.GetTopLevel(this) as Window;

        if (_window is not null)
        {
            _window.PropertyChanged += OnWindowPropertyChanged;
            UpdateWindowState();

            if (!IsSet(TitleProperty))
            {
                this[!TitleProperty] = _window[!Window.TitleProperty];
            }
        }
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_window is not null)
        {
            _window.PropertyChanged -= OnWindowPropertyChanged;
            _window = null;
        }

        base.OnDetachedFromVisualTree(e);
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Window.WindowStateProperty)
        {
            UpdateWindowState();
        }
    }

    private void UpdateWindowState()
    {
        IsMaximized = _window?.WindowState == WindowState.Maximized;
    }

    private void OnMinimizeClick(object? sender, RoutedEventArgs e)
    {
        _window?.WindowState = WindowState.Minimized;
    }

    private void OnMaximizeClick(object? sender, RoutedEventArgs e)
    {
        _window?.WindowState = IsMaximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        _window?.Close();
    }
}
