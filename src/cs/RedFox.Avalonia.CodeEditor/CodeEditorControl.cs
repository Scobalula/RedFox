using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;
using AvaloniaEdit.Search;
using AvaloniaEdit.TextMate;

namespace RedFox.Avalonia.CodeEditor;

/// <summary>
/// Provides a VS Code styled viewer and editor for text assets, with TextMate highlighting.
/// </summary>
public sealed class CodeEditorControl : UserControl, IDisposable
{
    /// <summary>
    /// Defines the <see cref="Text"/> property.
    /// </summary>
    public static readonly StyledProperty<string> TextProperty = AvaloniaProperty.Register<CodeEditorControl, string>(nameof(Text), string.Empty, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Defines the <see cref="FilePath"/> property.
    /// </summary>
    public static readonly StyledProperty<string> FilePathProperty = AvaloniaProperty.Register<CodeEditorControl, string>(nameof(FilePath), string.Empty);

    /// <summary>
    /// Defines the <see cref="IsReadOnly"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> IsReadOnlyProperty = AvaloniaProperty.Register<CodeEditorControl, bool>(nameof(IsReadOnly));

    /// <summary>
    /// Defines the <see cref="Colors"/> property.
    /// </summary>
    public static readonly StyledProperty<CodeEditorColors?> ColorsProperty = AvaloniaProperty.Register<CodeEditorControl, CodeEditorColors?>(nameof(Colors));

    /// <summary>
    /// Defines the <see cref="CodeFontFamily"/> property.
    /// </summary>
    public static readonly StyledProperty<FontFamily> CodeFontFamilyProperty = AvaloniaProperty.Register<CodeEditorControl, FontFamily>(nameof(CodeFontFamily), new FontFamily(DefaultCodeFont));

    /// <summary>
    /// Defines the <see cref="CodeFontSize"/> property.
    /// </summary>
    public static readonly StyledProperty<double> CodeFontSizeProperty = AvaloniaProperty.Register<CodeEditorControl, double>(nameof(CodeFontSize), 14.0);

    private const string DefaultCodeFont = "Consolas, Cascadia Mono, Menlo, monospace";
    private const double GutterInset = 10.0;
    private const double GutterLineGap = 12.0;
    private const double TopInset = 10.0;

    private static readonly TimeSpan AnalysisDelay = TimeSpan.FromMilliseconds(200);
    private static readonly Uri EditorStyles = new("avares://AvaloniaEdit/Themes/Fluent/AvaloniaEdit.xaml");

    private readonly TextEditor _editor = new();
    private readonly LineWidthGenerator _lineWidth = new();
    private readonly IndentGuideRenderer _indentGuides;
    private readonly BracketMatchRenderer _bracketMatch;
    private readonly Border _gutterLine = new() { Width = 1.0, Margin = new Thickness(GutterLineGap, 0, 0, 0) };
    private readonly BracketPairColorizer _brackets = new();
    private readonly DispatcherTimer _analysisTimer = new() { Interval = AnalysisDelay };
    private readonly WordHighlightRenderer _wordHighlight;
    private readonly SelectionRenderer _selection;
    private readonly ActiveLineNumberMargin _lineNumbers = new();
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
    /// Gets or sets the document text.
    /// </summary>
    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>
    /// Gets or sets the document path, which selects the grammar.
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

        _editor.FontFamily						= CodeFontFamily;
        _editor.FontSize						= CodeFontSize;
        _editor.Options.EnableHyperlinks		= false;
        _editor.Options.EnableEmailHyperlinks	= false;
        _editor.Options.HighlightCurrentLine	= true;
        _indentGuides							= new IndentGuideRenderer(_editor.TextArea);
        _editor.Options.IndentationSize			= _indentGuides.TabSize;
        _editor.TextArea.TextView.LineTransformers.Add(_brackets);

        _selection = new SelectionRenderer(_editor.TextArea);
        _editor.TextArea.TextView.BackgroundRenderers.Add(_selection);
        _editor.TextArea.TextView.BackgroundRenderers.Add(_indentGuides);

        _bracketMatch = new BracketMatchRenderer(_editor.TextArea);
        _editor.TextArea.TextView.BackgroundRenderers.Add(_bracketMatch);

        _wordHighlight = new WordHighlightRenderer(_editor.TextArea);
        _editor.TextArea.TextView.BackgroundRenderers.Add(_wordHighlight);

        _editor.TextArea.LeftMargins.Clear();
        _editor.TextArea.LeftMargins.Add(new Control { Width = GutterInset });
        _editor.TextArea.LeftMargins.Add(_lineNumbers);
        _editor.TextArea.LeftMargins.Add(_gutterLine);
        _editor.TextArea.TextView.ElementGenerators.Add(_lineWidth);
        _editor.TextArea.TextView.ScrollOffsetChanged += (_, _) => ApplyTopGap();

        _textMate = _editor.InstallTextMate(languages);
        _textMate.SetTheme(languages.GetDefaultTheme());
        _folding	= FoldingManager.Install(_editor.TextArea);
        _search		= SearchPanel.Install(_editor);

        _editor.TextArea.AddHandler(PointerPressedEvent, OnTextAreaPointerPressed, RoutingStrategies.Tunnel);
        _editor.TextChanged += OnEditorTextChanged;
        _editor.TextArea.Caret.PositionChanged += (_, _) => OnCaretOrSelectionChanged();
        _editor.TextArea.SelectionChanged += (_, _) => OnCaretOrSelectionChanged();
        _editor.TextArea.GotFocus += (_, _) => OnCaretOrSelectionChanged();
        _editor.TextArea.LostFocus += (_, _) => OnCaretOrSelectionChanged();
        _analysisTimer.Tick += (_, _) => Analyze();

        KeyBindings.Add(new KeyBinding
        {
            Gesture	= new KeyGesture(Key.S, KeyModifiers.Control),
            Command	= new CodeEditorSaveCommand(() => SaveRequested?.Invoke(this, EventArgs.Empty)),
        });


        Content = _editor;

        ActualThemeVariantChanged += (_, _) => ApplyColors();
    }

    /// <summary>
    /// Marks the current text as the saved state.
    /// </summary>
    public void MarkSaved()
    {
        _editor.Document.UndoStack.MarkAsOriginalFile();
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
            Analyze();
        }
        else if (change.Property == IsReadOnlyProperty)
        {
            _editor.IsReadOnly					= change.GetNewValue<bool>();
            _editor.Options.EnableTextDragDrop	= !IsReadOnly;
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

        _isUpdatingText			= true;
        _editor.Document.Text	= text;
        _editor.Document.UndoStack.ClearAll();
        _editor.Document.UndoStack.MarkAsOriginalFile();
        _editor.CaretOffset = 0;
        _editor.ScrollToHome();
        _isUpdatingText = false;
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

    private void OnTextAreaPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        AvaloniaEdit.Rendering.TextView textView = _editor.TextArea.TextView;
        if (e.ClickCount != 2 || !e.GetCurrentPoint(textView).Properties.IsLeftButtonPressed || !textView.Bounds.Contains(e.GetPosition(textView.Parent as Visual)))
        {
            return;
        }

        TextViewPosition? position = textView.GetPositionFloor(e.GetPosition(textView) + textView.ScrollOffset);
        if (position is not TextViewPosition { Line: > 0 } target)
        {
            return;
        }

        DocumentLine line = _editor.Document.GetLineByNumber(target.Line);
        (int start, int length) = WordOccurrences.FindRun(_editor.Document.GetText(line.Offset, line.Length), target.Column - 1);

        _editor.TextArea.Focus();
        _editor.Select(line.Offset + start, length);
        e.Handled = true;
    }

    private void ApplyTopGap()
    {
        Thickness margin = new(0, Math.Max(TopInset - _editor.TextArea.TextView.ScrollOffset.Y, 0), 0, 0);

        _editor.TextArea.TextView.Margin	= margin;
        _lineNumbers.Margin					= margin;

        foreach (FoldingMargin foldingMargin in _editor.TextArea.LeftMargins.OfType<FoldingMargin>())
        {
            foldingMargin.Margin = margin;
        }
    }

    private void OnCaretOrSelectionChanged()
    {
        _lineNumbers.ActiveLine = _editor.TextArea.Caret.Line;
        _lineNumbers.InvalidateVisual();
        _editor.TextArea.TextView.InvalidateLayer(_wordHighlight.Layer);
        _editor.TextArea.TextView.InvalidateLayer(_indentGuides.Layer);
    }

    private void SelectGrammar()
    {
        _textMate.SetGrammar(Languages.FindScope(FilePath) ?? CodeLanguageRegistry.PlainScope);
    }

    private void ApplyColors()
    {
        CodeEditorColors colors = Colors ?? CodeEditorColors.FromResources(this);

        _editor.Background								= new SolidColorBrush(colors.Background);
        _editor.Foreground								= new SolidColorBrush(colors.Foreground);
        _lineNumbers.LineForeground						= new SolidColorBrush(colors.LineNumber);
        _lineNumbers.ActiveForeground					= new SolidColorBrush(colors.ActiveLineNumber);
        _lineNumbers.ActiveLine							= _editor.TextArea.Caret.Line;
        _wordHighlight.Brush							= new SolidColorBrush(colors.WordHighlight);
        _selection.Brush								= new SolidColorBrush(colors.Selection);
        _selection.WhitespaceBrush						= new SolidColorBrush(colors.SelectionWhitespace);
        _editor.TextArea.SelectionBrush					= Brushes.Transparent;
        _editor.TextArea.SelectionBorder				= null;
        _editor.TextArea.SelectionForeground			= null;
        _editor.TextArea.Caret.CaretBrush				= new SolidColorBrush(colors.Caret);
        _editor.TextArea.TextView.CurrentLineBackground	= Brushes.Transparent;
        _editor.TextArea.TextView.CurrentLineBorder		= new Pen(new SolidColorBrush(colors.CurrentLine), 2);
        _indentGuides.Pen								= new Pen(new SolidColorBrush(colors.IndentGuide), 1);
        _indentGuides.ActivePen							= new Pen(new SolidColorBrush(colors.ActiveIndentGuide), 1);
        _gutterLine.Background							= new SolidColorBrush(colors.GutterLine);
        _bracketMatch.Brush								= new SolidColorBrush(colors.BracketMatch);
        _bracketMatch.Pen								= new Pen(new SolidColorBrush(colors.BracketMatchBorder), 1);

        foreach (FoldingMargin margin in _editor.TextArea.LeftMargins.OfType<FoldingMargin>())
        {
            margin.FoldingMarkerBrush					= Brushes.Transparent;
            margin.FoldingMarkerBackgroundBrush			= Brushes.Transparent;
            margin.SelectedFoldingMarkerBrush			= new SolidColorBrush(colors.LineNumber);
            margin.SelectedFoldingMarkerBackgroundBrush	= Brushes.Transparent;
        }

        Analyze();
    }

    private void Analyze()
    {
        _analysisTimer.Stop();

        CodeEditorColors colors		= Colors ?? CodeEditorColors.FromResources(this);
        BracketSyntax? syntax		= Languages.FindScope(FilePath) is null ? null : BracketSyntax.ForFile(FilePath);
        List<BracketToken> brackets	= syntax is null ? [] : BracketScanner.Scan(_editor.Text, syntax);

        _brackets.Update(brackets, [.. colors.Brackets.Select(color => new SolidColorBrush(color))]);
        _bracketMatch.Pairs = BracketPairs.Build(brackets);
        _folding.UpdateFoldings(CreateFoldings(brackets), -1);
        _lineWidth.Columns = _editor.Document.Lines.Select(line => IndentGuides.GetColumnCount(_editor.Document.GetText(line.Offset, line.Length), _lineWidth.TabSize)).DefaultIfEmpty().Max();
        ApplyTopGap();
        _editor.TextArea.TextView.Redraw();
    }

    private IEnumerable<NewFolding> CreateFoldings(List<BracketToken> brackets)
    {
        TextDocument document		= _editor.Document;
        Stack<BracketToken> open	= new();
        List<NewFolding> foldings	= [];

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
}
