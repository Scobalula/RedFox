using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using ShapePath = Avalonia.Controls.Shapes.Path;

namespace RedFox.Avalonia.CodeEditor;

/// <summary>
/// Shows the document's tab and the breadcrumb path beneath it.
/// </summary>
internal sealed class CodeEditorHeader : UserControl
{
    private static readonly Geometry FileIcon = Geometry.Parse("M4,2 L9.5,2 L13,5.5 L13,14 L3,14 L3,2 Z");
    private static readonly Geometry CloseIcon = Geometry.Parse("M3.4,2.3 L8,6.9 L12.6,2.3 L13.7,3.4 L9.1,8 L13.7,12.6 L12.6,13.7 L8,9.1 L3.4,13.7 L2.3,12.6 L6.9,8 L2.3,3.4 Z");

    private readonly Border _tabBar = new();
    private readonly Border _tab = new() { Padding = new Thickness(10, 0, 8, 0), Height = 35 };
    private readonly TextBlock _title = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 10, 0), FontSize = 13 };
    private readonly ShapePath _tabIcon = new() { Data = FileIcon, Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Center };
    private readonly ShapePath _close = new() { Data = CloseIcon, Width = 16, Height = 16, Stretch = Stretch.None, Cursor = new Cursor(StandardCursorType.Hand), VerticalAlignment = VerticalAlignment.Center };
    private readonly StackPanel _breadcrumbs = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(12, 0), Height = 22 };
    private readonly Border _breadcrumbBar = new();

    private CodeEditorColors _colors = CodeEditorColors.VsCodeDark;
    private string _filePath = string.Empty;
    private bool _isModified;

    /// <summary>
    /// Occurs when the tab's close button is pressed.
    /// </summary>
    public event EventHandler? CloseRequested;

    /// <summary>
    /// Initializes the header.
    /// </summary>
    public CodeEditorHeader()
    {
        StackPanel tabContent = new() { Orientation = Orientation.Horizontal };
        tabContent.Children.Add(_tabIcon);
        tabContent.Children.Add(_title);
        tabContent.Children.Add(_close);

        _tab.Child = tabContent;
        _tab.HorizontalAlignment = HorizontalAlignment.Left;
        _tabBar.Child = _tab;
        _breadcrumbBar.Child = _breadcrumbs;
        _close.PointerPressed += OnClosePressed;

        DockPanel panel = new();
        DockPanel.SetDock(_tabBar, Dock.Top);
        panel.Children.Add(_tabBar);
        panel.Children.Add(_breadcrumbBar);
        Content = panel;
    }

    /// <summary>
    /// Sets the document shown by the header.
    /// </summary>
    /// <param name="filePath">The document path.</param>
    /// <param name="isModified">Whether the document has unsaved changes.</param>
    public void SetDocument(string filePath, bool isModified)
    {
        _filePath = filePath;
        _isModified = isModified;
        Rebuild();
    }

    /// <summary>
    /// Applies a palette.
    /// </summary>
    /// <param name="colors">The palette.</param>
    public void Apply(CodeEditorColors colors)
    {
        _colors = colors;
        Rebuild();
    }

    /// <summary>
    /// Splits a document path into the parts the breadcrumbs show.
    /// </summary>
    /// <param name="filePath">The document path.</param>
    /// <returns>The path parts.</returns>
    public static string[] GetParts(string filePath)
    {
        return filePath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
    }

    private void Rebuild()
    {
        IBrush foreground = new SolidColorBrush(_colors.TabForeground);
        IBrush muted = new SolidColorBrush(_colors.BreadcrumbForeground);
        IBrush accent = new SolidColorBrush(_colors.Accent);

        _tabBar.Background = new SolidColorBrush(_colors.TabBarBackground);
        _tab.Background = new SolidColorBrush(_colors.TabBackground);
        _breadcrumbBar.Background = new SolidColorBrush(_colors.Background);
        _title.Text = (_isModified ? "● " : string.Empty) + Path.GetFileName(_filePath);
        _title.Foreground = foreground;
        _tabIcon.Fill = accent;
        _close.Fill = foreground;

        string[] parts = GetParts(_filePath);
        _breadcrumbs.Children.Clear();

        for (int index = 0; index < parts.Length; index++)
        {
            if (index > 0)
            {
                _breadcrumbs.Children.Add(CreateCrumb(">", muted));
            }

            if (index == parts.Length - 1)
            {
                _breadcrumbs.Children.Add(new ShapePath { Data = FileIcon, Fill = accent, Width = 14, Height = 14, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
            }

            _breadcrumbs.Children.Add(CreateCrumb(parts[index], muted));
        }
    }

    private static TextBlock CreateCrumb(string text, IBrush foreground)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = foreground,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0),
        };
    }

    private void OnClosePressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
