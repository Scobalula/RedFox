using Avalonia;
using Avalonia.Controls;

namespace RedFox.GameExtraction.UI.Controls;

public partial class TextEditorControl : UserControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<TextEditorControl, string?>(nameof(Text));

    public static readonly StyledProperty<string?> LanguageProperty =
        AvaloniaProperty.Register<TextEditorControl, string?>(nameof(Language));

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string? Language
    {
        get => GetValue(LanguageProperty);
        set => SetValue(LanguageProperty, value);
    }

    public TextEditorControl()
    {
        InitializeComponent();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TextProperty)
        {
            Editor.Text = change.GetNewValue<string?>() ?? string.Empty;
        }
    }
}
