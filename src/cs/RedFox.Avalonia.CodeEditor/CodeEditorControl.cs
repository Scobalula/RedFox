using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;
using AvaloniaEdit.Search;
using AvaloniaEdit.TextMate;

namespace RedFox.Avalonia.CodeEditor;

/// <summary>
/// Provides a VS Code styled viewer and editor for text assets, with a tab, breadcrumbs and TextMate highlighting.
/// </summary>
public sealed class CodeEditorControl : UserControl, IDisposable
{
    /// <summary>
    /// Defines the <see cref="Text"/> property.
    /// </summary>
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<CodeEditorControl, string>(nameof(Text), string.Empty, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Defines the <see cref="FilePath"/> property.
    /// </summary>
    public static readonly StyledProperty<string> FilePathProperty =
        AvaloniaProperty.Register<CodeEditorControl, string>(nameof(FilePath), string.Empty);

    /// <summary>
    /// Defines the <see cref="IsReadOnly"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<CodeEditorControl, bool>(nameof(IsReadOnly));

    /// <summary>
    /// Defines the <see cref="ShowHeader"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> ShowHeaderProperty =
        AvaloniaProperty.Register<CodeEditorControl, bool>(nameof(ShowHeader), defaultValue: true);

    /// <summary>
    /// Defines the <see cref="Colors"/> property.
    /// </summary>
    public static readonly StyledProperty<CodeEditorColors?> ColorsProperty =
        AvaloniaProperty.Register<CodeEditorControl, CodeEditorColors?>(nameof(Colors));

    /// <summary>
    /// Defines the <see cref="CodeFontFamily"/> property.
    /// </summary>
    public static readonly StyledProperty<FontFamily> CodeFontFamilyProperty =
        AvaloniaProperty.Register<CodeEditorControl, FontFamily>(nameof(CodeFontFamily), new FontFamily("Consolas, Cascadia Mono, Menlo, monospace"));

    /// <summary>
    /// Defines the <see cref="CodeFontSize"/> property.
    /// </summary>
    public static readonly StyledProperty<double> CodeFontSizeProperty =
        AvaloniaProperty.Register<CodeEditorControl, double>(nameof(CodeFontSize), 14.0);

    private const double GutterInset = 10.0;
    private const double TextInset = 6.0;

    private static readonly TimeSpan AnalysisDelay = TimeSpan.FromMilliseconds(200);
    private static readonly Uri EditorStyles = new("avares://AvaloniaEdit/Themes/Fluent/AvaloniaEdit.xaml");

    private readonly TextEditor _editor = new();
    private readonly CodeEditorHeader _header = new();
    private readonly IndentGuideRenderer _indentGuides = new();
    private readonly BracketPairColorizer _brackets = new();
    private readonly DispatcherTimer _analysisTimer = new() { Interval = AnalysisDelay };
    private readonly TextMate.Installation _textMate;
    private readonly FoldingManager _folding;
    private readonly SearchPanel _search;

    private bool _isUpdatingText;
    private bool _isDisposed;

    /// <summary>
    /// Occurs when the user saves with Ctrl+S.
    /// </summary>
    public event EventHandler? SaveRequested;

    /// <summary>
    /// Occurs when the user closes the tab.
    /// </summary>
    public event EventHandler? CloseRequested;

    /// <summary>
    /// Gets or sets the document text.
    /// </summary>
    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>
    /// Gets or sets the document path, which names the tab, fills the breadcrumbs and selects the grammar.
    /// </summary>
    public string FilePath
    {
        get => GetValue(FilePathProperty);
        set => SetValue(FilePathProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the document can be changed.
    /// </summary>
    public bool IsReadOnly
    {
        get => GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the tab and breadcrumbs are shown.
    /// </summary>
    public bool ShowHeader
    {
        get => GetValue(ShowHeaderProperty);
        set => SetValue(ShowHeaderProperty, value);
    }

    /// <summary>
    /// Gets or sets the palette, or <see langword="null"/> to follow the host application's theme.
    /// </summary>
    public CodeEditorColors? Colors
    {
        get => GetValue(ColorsProperty);
        set => SetValue(ColorsProperty, value);
    }

    /// <summary>
    /// Gets or sets the code font.
    /// </summary>
    public FontFamily CodeFontFamily
    {
        get => GetValue(CodeFontFamilyProperty);
        set => SetValue(CodeFontFamilyProperty, value);
    }

    /// <summary>
    /// Gets or sets the code font size.
    /// </summary>
    public double CodeFontSize
    {
        get => GetValue(CodeFontSizeProperty);
        set => SetValue(CodeFontSizeProperty, value);
    }

    /// <summary>
    /// Gets a value indicating whether the document differs from its last saved state.
    /// </summary>
    public bool IsModified => !_editor.Document.UndoStack.IsOriginalFile;

    /// <summary>
    /// Gets the registry that resolves grammars.
    /// </summary>
    public CodeLanguageRegistry Languages { get; }

    /// <summary>
    /// Initializes an editor that uses <see cref="CodeLanguageRegistry.Default"/>.
    /// </summary>
    public CodeEditorControl() : this(CodeLanguageRegistry.Default)
    {
    }

    /// <summary>
    /// Initializes an editor with its own grammars.
    /// </summary>
    /// <param name="languages">The grammars.</param>
    public CodeEditorControl(CodeLanguageRegistry languages)
    {
        ArgumentNullException.ThrowIfNull(languages);
        Languages = languages;

        Styles.Add(new StyleInclude(EditorStyles) { Source = EditorStyles });

        _editor.ShowLineNumbers = true;
        _editor.FontFamily = CodeFontFamily;
        _editor.FontSize = CodeFontSize;
        _editor.Options.EnableHyperlinks = false;
        _editor.Options.EnableEmailHyperlinks = false;
        _editor.Options.HighlightCurrentLine = true;
        _editor.Options.IndentationSize = _indentGuides.TabSize;
        _editor.TextArea.SelectionCornerRadius = 0;
        _editor.TextArea.TextView.BackgroundRenderers.Add(_indentGuides);
        _editor.TextArea.TextView.LineTransformers.Add(_brackets);
        _editor.TextArea.TextView.Margin = new Thickness(TextInset, 0, 0, 0);
        _editor.TextArea.LeftMargins.Insert(0, new Control { Width = GutterInset });

        _textMate = _editor.InstallTextMate(languages);
        _textMate.SetTheme(languages.GetDefaultTheme());
        _folding = FoldingManager.Install(_editor.TextArea);
        _search = SearchPanel.Install(_editor);

        _editor.TextChanged += OnEditorTextChanged;
        _editor.Document.UndoStack.PropertyChanged += (_, _) => UpdateHeader();
        _analysisTimer.Tick += (_, _) => Analyze();
        _header.CloseRequested += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);

        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.S, KeyModifiers.Control), Command = new SaveCommand(this) });

        DockPanel panel = new();
        DockPanel.SetDock(_header, Dock.Top);
        panel.Children.Add(_header);
        panel.Children.Add(_editor);
        Content = panel;

        ActualThemeVariantChanged += (_, _) => ApplyColors();
    }

    /// <summary>
    /// Marks the current text as the saved state.
    /// </summary>
    public void MarkSaved()
    {
        _editor.Document.UndoStack.MarkAsOriginalFile();
        UpdateHeader();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _analysisTimer.Stop();
        _search.Uninstall();
        FoldingManager.Uninstall(_folding);
        _textMate.Dispose();
    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ApplyColors();
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TextProperty)
        {
            SetDocumentText(change.GetNewValue<string>());
        }
        else if (change.Property == FilePathProperty)
        {
            SelectGrammar();
            UpdateHeader();
            Analyze();
        }
        else if (change.Property == IsReadOnlyProperty)
        {
            _editor.IsReadOnly = change.GetNewValue<bool>();
        }
        else if (change.Property == ShowHeaderProperty)
        {
            _header.IsVisible = change.GetNewValue<bool>();
        }
        else if (change.Property == ColorsProperty)
        {
            ApplyColors();
        }
        else if (change.Property == CodeFontFamilyProperty)
        {
            _editor.FontFamily = change.GetNewValue<FontFamily>();
        }
        else if (change.Property == CodeFontSizeProperty)
        {
            _editor.FontSize = change.GetNewValue<double>();
        }
    }

    private void SetDocumentText(string text)
    {
        if (_isUpdatingText)
        {
            return;
        }

        _isUpdatingText = true;
        _editor.Document.Text = text;
        _editor.Document.UndoStack.ClearAll();
        _editor.Document.UndoStack.MarkAsOriginalFile();
        _isUpdatingText = false;
        UpdateHeader();
        Analyze();
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingText)
        {
            return;
        }

        _isUpdatingText = true;
        SetCurrentValue(TextProperty, _editor.Text);
        _isUpdatingText = false;
        _analysisTimer.Stop();
        _analysisTimer.Start();
    }

    private void SelectGrammar()
    {
        string? scope = Languages.FindScope(FilePath);
        if (scope is not null)
        {
            _textMate.SetGrammar(scope);
        }
    }

    private void UpdateHeader()
    {
        _header.SetDocument(FilePath, IsModified);
    }

    private void ApplyColors()
    {
        CodeEditorColors colors = Colors ?? CodeEditorColors.FromResources(this);

        _editor.Background = new SolidColorBrush(colors.Background);
        _editor.Foreground = new SolidColorBrush(colors.Foreground);
        _editor.LineNumbersForeground = new SolidColorBrush(colors.LineNumber);
        _editor.TextArea.SelectionBrush = new SolidColorBrush(colors.Selection);
        _editor.TextArea.SelectionForeground = null;
        _editor.TextArea.Caret.CaretBrush = new SolidColorBrush(colors.Caret);
        _editor.TextArea.TextView.CurrentLineBackground = Brushes.Transparent;
        _editor.TextArea.TextView.CurrentLineBorder = new Pen(new SolidColorBrush(colors.CurrentLine), 2);
        _indentGuides.Pen = new Pen(new SolidColorBrush(colors.IndentGuide), 1);

        // Fold markers stay hidden until the pointer is over them, as in VS Code.
        foreach (FoldingMargin margin in _editor.TextArea.LeftMargins.OfType<FoldingMargin>())
        {
            margin.FoldingMarkerBrush = Brushes.Transparent;
            margin.FoldingMarkerBackgroundBrush = Brushes.Transparent;
            margin.SelectedFoldingMarkerBrush = new SolidColorBrush(colors.LineNumber);
            margin.SelectedFoldingMarkerBackgroundBrush = Brushes.Transparent;
        }

        _header.Apply(colors);
        Analyze();
    }

    private void Analyze()
    {
        _analysisTimer.Stop();

        CodeEditorColors colors = Colors ?? CodeEditorColors.FromResources(this);
        BracketSyntax? syntax = BracketSyntax.ForFile(FilePath);
        List<BracketToken> brackets = syntax is null ? [] : BracketScanner.Scan(_editor.Text, syntax);

        _brackets.Update(brackets, [.. colors.Brackets.Select(color => new SolidColorBrush(color))]);
        _folding.UpdateFoldings(CreateFoldings(brackets), -1);
        _editor.TextArea.TextView.Redraw();
    }

    private IEnumerable<NewFolding> CreateFoldings(List<BracketToken> brackets)
    {
        TextDocument document = _editor.Document;
        Stack<BracketToken> open = new();
        List<NewFolding> foldings = [];

        foreach (BracketToken bracket in brackets.Where(bracket => bracket.Character is '{' or '}'))
        {
            if (bracket.Character == '{')
            {
                open.Push(bracket);
            }
            else if (open.TryPop(out BracketToken start) && document.GetLineByOffset(start.Offset).LineNumber != document.GetLineByOffset(bracket.Offset).LineNumber)
            {
                foldings.Add(new NewFolding(start.Offset, bracket.Offset + 1));
            }
        }

        return foldings.OrderBy(folding => folding.StartOffset);
    }

    private void RaiseSaveRequested()
    {
        SaveRequested?.Invoke(this, EventArgs.Empty);
    }

    private sealed class SaveCommand(CodeEditorControl editor) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => editor.RaiseSaveRequested();
    }
}
