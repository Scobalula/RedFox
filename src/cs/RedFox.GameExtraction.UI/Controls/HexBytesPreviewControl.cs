using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace RedFox.GameExtraction.UI.Controls;

/// <summary>
/// Displays byte arrays as a read-only hex and ASCII grid with internal row virtualization.
/// </summary>
public sealed class HexBytesPreviewControl : UserControl
{
    private const int DefaultBytesPerRow = 16;
    internal const double HeaderHeight = 28;
    internal const double RowHeight = 22;
    internal const double LeftPadding = 12;
    internal const double TopPadding = 8;
    internal const double OffsetGap = 18;
    internal const double AsciiGap = 24;
    internal const double CharWidth = 8.2;
    internal const double TextFontSize = 13;

    internal static readonly Typeface HexTypeface = new("Consolas");
    internal static readonly IBrush BackgroundBrush = Brush.Parse("#141417");
    internal static readonly IBrush HeaderBackgroundBrush = Brush.Parse("#1E1E22");
    internal static readonly IBrush AlternateRowBrush = Brush.Parse("#1A1A1E");
    internal static readonly IBrush HeaderTextBrush = Brush.Parse("#909096");
    internal static readonly IBrush OffsetTextBrush = Brush.Parse("#6DB6FF");
    internal static readonly IBrush HexTextBrush = Brush.Parse("#E8E8EA");
    internal static readonly IBrush AsciiTextBrush = Brush.Parse("#D69D85");
    internal static readonly IBrush BorderStrokeBrush = Brush.Parse("#2C2C31");

    private readonly ScrollBar _horizontalScrollBar;
    private readonly ScrollBar _verticalScrollBar;
    private readonly HexBytesViewportControl _viewport;
    private int _firstVisibleRow;
    private double _horizontalOffset;
    private bool _updatingScrollBars;

    /// <summary>
    /// Defines the bytes displayed by the control.
    /// </summary>
    public static readonly StyledProperty<byte[]?> BytesProperty =
        AvaloniaProperty.Register<HexBytesPreviewControl, byte[]?>(nameof(Bytes));

    /// <summary>
    /// Defines the number of bytes displayed in each row.
    /// </summary>
    public static readonly StyledProperty<int> BytesPerRowProperty =
        AvaloniaProperty.Register<HexBytesPreviewControl, int>(nameof(BytesPerRow), DefaultBytesPerRow);

    static HexBytesPreviewControl()
    {
        BytesProperty.Changed.AddClassHandler<HexBytesPreviewControl>((control, _) => control.OnPreviewBytesChanged());
        BytesPerRowProperty.Changed.AddClassHandler<HexBytesPreviewControl>((control, _) => control.OnPreviewBytesChanged());
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HexBytesPreviewControl"/> class.
    /// </summary>
    public HexBytesPreviewControl()
    {
        _viewport = new HexBytesViewportControl(this);
        _viewport.PointerWheelChanged += OnViewportPointerWheelChanged;
        _viewport.SizeChanged += OnViewportSizeChanged;

        _verticalScrollBar = new ScrollBar
        {
            Orientation = Orientation.Vertical,
            Width = 14,
            Minimum = 0,
            SmallChange = 1,
            AllowAutoHide = false,
        };
        _verticalScrollBar.PropertyChanged += OnVerticalScrollBarPropertyChanged;

        _horizontalScrollBar = new ScrollBar
        {
            Orientation = Orientation.Horizontal,
            Height = 14,
            Minimum = 0,
            SmallChange = 16,
            AllowAutoHide = false,
        };
        _horizontalScrollBar.PropertyChanged += OnHorizontalScrollBarPropertyChanged;

        Border viewportBorder = new()
        {
            Background = BackgroundBrush,
            BorderBrush = BorderStrokeBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            ClipToBounds = true,
            Child = _viewport,
        };

        Grid root = new()
        {
            RowDefinitions = new RowDefinitions("*,Auto"),
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
        };
        Grid.SetRow(viewportBorder, 0);
        Grid.SetColumn(viewportBorder, 0);
        Grid.SetRow(_verticalScrollBar, 0);
        Grid.SetColumn(_verticalScrollBar, 1);
        Grid.SetRow(_horizontalScrollBar, 1);
        Grid.SetColumn(_horizontalScrollBar, 0);

        root.Children.Add(viewportBorder);
        root.Children.Add(_verticalScrollBar);
        root.Children.Add(_horizontalScrollBar);
        Content = root;

        MinHeight = 180;
        MinWidth = 320;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        UpdateScrollBars();
    }

    /// <summary>
    /// Gets or sets the bytes displayed by the control.
    /// </summary>
    public byte[]? Bytes
    {
        get => GetValue(BytesProperty);
        set => SetValue(BytesProperty, value);
    }

    /// <summary>
    /// Gets or sets the number of bytes displayed in each row.
    /// </summary>
    public int BytesPerRow
    {
        get => GetValue(BytesPerRowProperty);
        set => SetValue(BytesPerRowProperty, value);
    }

    internal byte[]? PreviewBytes => Bytes;

    internal int RowCount
    {
        get
        {
            int bytesPerRow = GetBytesPerRow();
            return PreviewBytes is { Length: > 0 } bytes
                ? (bytes.Length + bytesPerRow - 1) / bytesPerRow
                : 0;
        }
    }

    internal int VisibleRowCount
    {
        get
        {
            double contentHeight = Math.Max(0, _viewport.Bounds.Height - HeaderHeight - (TopPadding * 2));
            return Math.Max(1, (int)Math.Ceiling(contentHeight / RowHeight));
        }
    }

    internal int FirstVisibleRow => _firstVisibleRow;

    internal double HorizontalOffset => _horizontalOffset;

    private void OnPreviewBytesChanged()
    {
        _firstVisibleRow = 0;
        _horizontalOffset = 0;
        UpdateScrollBars();
        _viewport.InvalidateVisual();
    }

    private void OnViewportPointerWheelChanged(object? sender, PointerWheelEventArgs args)
    {
        if (args.KeyModifiers.HasFlag(KeyModifiers.Shift) && _horizontalScrollBar.IsVisible)
        {
            SetHorizontalOffset(_horizontalOffset - (args.Delta.Y * 42));
            args.Handled = true;
            return;
        }

        if (_verticalScrollBar.IsVisible)
        {
            SetFirstVisibleRow(_firstVisibleRow - (int)Math.Round(args.Delta.Y * 3));
            args.Handled = true;
        }
    }

    private void OnViewportSizeChanged(object? sender, SizeChangedEventArgs args)
    {
        UpdateScrollBars();
    }

    private void OnVerticalScrollBarPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
    {
        if (_updatingScrollBars || args.Property != RangeBase.ValueProperty)
        {
            return;
        }

        SetFirstVisibleRow((int)Math.Round(args.GetNewValue<double>()));
    }

    private void OnHorizontalScrollBarPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
    {
        if (_updatingScrollBars || args.Property != RangeBase.ValueProperty)
        {
            return;
        }

        SetHorizontalOffset(args.GetNewValue<double>());
    }

    private void SetFirstVisibleRow(int value)
    {
        int maxValue = Math.Max(0, RowCount - VisibleRowCount);
        int clamped = Math.Clamp(value, 0, maxValue);
        if (_firstVisibleRow == clamped)
        {
            return;
        }

        _firstVisibleRow = clamped;
        UpdateScrollBars();
        _viewport.InvalidateVisual();
    }

    private void SetHorizontalOffset(double value)
    {
        double maxValue = Math.Max(0, GetContentWidth(PreviewBytes, GetBytesPerRow()) - _viewport.Bounds.Width);
        double clamped = Math.Clamp(value, 0, maxValue);
        if (Math.Abs(_horizontalOffset - clamped) < 0.1)
        {
            return;
        }

        _horizontalOffset = clamped;
        UpdateScrollBars();
        _viewport.InvalidateVisual();
    }

    private void UpdateScrollBars()
    {
        _updatingScrollBars = true;

        try
        {
            int visibleRows = VisibleRowCount;
            int maxFirstRow = Math.Max(0, RowCount - visibleRows);
            _firstVisibleRow = Math.Clamp(_firstVisibleRow, 0, maxFirstRow);

            _verticalScrollBar.IsVisible = true;
            _verticalScrollBar.Maximum = maxFirstRow;
            _verticalScrollBar.ViewportSize = visibleRows;
            _verticalScrollBar.SmallChange = 1;
            _verticalScrollBar.LargeChange = Math.Max(1, visibleRows - 1);
            _verticalScrollBar.Value = _firstVisibleRow;

            double maxHorizontalOffset = Math.Max(0, GetContentWidth(PreviewBytes, GetBytesPerRow()) - _viewport.Bounds.Width);
            _horizontalOffset = Math.Clamp(_horizontalOffset, 0, maxHorizontalOffset);
            _horizontalScrollBar.IsVisible = true;
            _horizontalScrollBar.Maximum = maxHorizontalOffset;
            _horizontalScrollBar.ViewportSize = Math.Max(0, _viewport.Bounds.Width);
            _horizontalScrollBar.SmallChange = 24;
            _horizontalScrollBar.LargeChange = Math.Max(64, _viewport.Bounds.Width * 0.5);
            _horizontalScrollBar.Value = _horizontalOffset;
        }
        finally
        {
            _updatingScrollBars = false;
        }
    }

    internal int GetBytesPerRow() => Math.Clamp(BytesPerRow, 1, 64);

    private static double GetContentWidth(byte[]? bytes, int bytesPerRow)
    {
        int offsetDigits = bytes is { Length: > 0 } ? GetOffsetDigitCount(bytes) : 8;
        return LeftPadding + offsetDigits * CharWidth + OffsetGap + bytesPerRow * 3 * CharWidth + AsciiGap + bytesPerRow * CharWidth + LeftPadding;
    }

    internal static int GetOffsetDigitCount(byte[] bytes) => Math.Max(8, (bytes.Length - 1).ToString("X", CultureInfo.InvariantCulture).Length);

    internal static string CreateHeader(int bytesPerRow)
    {
        StringBuilder builder = new(bytesPerRow * 3);
        for (int index = 0; index < bytesPerRow; index++)
        {
            if (index > 0)
            {
                builder.Append(' ');
            }

            builder.Append(index.ToString("X2", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    internal static string CreateHexRow(byte[] bytes, int offset, int count)
    {
        StringBuilder builder = new(count * 3);
        for (int index = 0; index < count; index++)
        {
            if (index > 0)
            {
                builder.Append(' ');
            }

            builder.Append(bytes[offset + index].ToString("X2", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    internal static string CreateAsciiRow(byte[] bytes, int offset, int count)
    {
        StringBuilder builder = new(count);
        for (int index = 0; index < count; index++)
        {
            byte value = bytes[offset + index];
            builder.Append(value is >= 32 and <= 126 ? (char)value : '.');
        }

        return builder.ToString();
    }

    internal static void DrawText(DrawingContext context, string text, IBrush brush, Point origin)
    {
        FormattedText formattedText = new(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            HexTypeface,
            TextFontSize,
            brush);

        context.DrawText(formattedText, origin);
    }

}
