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
    private const double HeaderHeight = 28;
    private const double RowHeight = 22;
    private const double LeftPadding = 12;
    private const double TopPadding = 8;
    private const double OffsetGap = 18;
    private const double AsciiGap = 24;
    private const double CharWidth = 8.2;
    private const double TextFontSize = 13;

    private static readonly Typeface HexTypeface = new("Consolas");
    private static readonly IBrush BackgroundBrush = Brush.Parse("#141417");
    private static readonly IBrush HeaderBackgroundBrush = Brush.Parse("#1E1E22");
    private static readonly IBrush AlternateRowBrush = Brush.Parse("#1A1A1E");
    private static readonly IBrush HeaderTextBrush = Brush.Parse("#909096");
    private static readonly IBrush OffsetTextBrush = Brush.Parse("#6DB6FF");
    private static readonly IBrush HexTextBrush = Brush.Parse("#E8E8EA");
    private static readonly IBrush AsciiTextBrush = Brush.Parse("#D69D85");
    private static readonly IBrush BorderStrokeBrush = Brush.Parse("#2C2C31");

    private readonly ScrollBar _horizontalScrollBar;
    private readonly ScrollBar _verticalScrollBar;
    private readonly ViewportControl _viewport;
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
        _viewport = new ViewportControl(this);
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

    private byte[]? PreviewBytes => Bytes;

    private int RowCount
    {
        get
        {
            int bytesPerRow = GetBytesPerRow();
            return PreviewBytes is { Length: > 0 } bytes
                ? (bytes.Length + bytesPerRow - 1) / bytesPerRow
                : 0;
        }
    }

    private int VisibleRowCount
    {
        get
        {
            double contentHeight = Math.Max(0, _viewport.Bounds.Height - HeaderHeight - (TopPadding * 2));
            return Math.Max(1, (int)Math.Ceiling(contentHeight / RowHeight));
        }
    }

    private int FirstVisibleRow => _firstVisibleRow;

    private double HorizontalOffset => _horizontalOffset;

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

    private int GetBytesPerRow() => Math.Clamp(BytesPerRow, 1, 64);

    private static double GetContentWidth(byte[]? bytes, int bytesPerRow)
    {
        int offsetDigits = bytes is { Length: > 0 } ? GetOffsetDigitCount(bytes) : 8;
        return LeftPadding + offsetDigits * CharWidth + OffsetGap + bytesPerRow * 3 * CharWidth + AsciiGap + bytesPerRow * CharWidth + LeftPadding;
    }

    private static int GetOffsetDigitCount(byte[] bytes) => Math.Max(8, (bytes.Length - 1).ToString("X", CultureInfo.InvariantCulture).Length);

    private static string CreateHeader(int bytesPerRow)
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

    private static string CreateHexRow(byte[] bytes, int offset, int count)
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

    private static string CreateAsciiRow(byte[] bytes, int offset, int count)
    {
        StringBuilder builder = new(count);
        for (int index = 0; index < count; index++)
        {
            byte value = bytes[offset + index];
            builder.Append(value is >= 32 and <= 126 ? (char)value : '.');
        }

        return builder.ToString();
    }

    private static void DrawText(DrawingContext context, string text, IBrush brush, Point origin)
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

    private sealed class ViewportControl : Control
    {
        private readonly HexBytesPreviewControl _owner;

        public ViewportControl(HexBytesPreviewControl owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            ClipToBounds = true;
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            Rect bounds = Bounds;
            context.DrawRectangle(BackgroundBrush, null, bounds);
            context.DrawRectangle(HeaderBackgroundBrush, null, new Rect(0, 0, bounds.Width, HeaderHeight));
            context.DrawLine(new Pen(BorderStrokeBrush, 1), new Point(0, HeaderHeight - 0.5), new Point(bounds.Width, HeaderHeight - 0.5));

            byte[]? bytes = _owner.PreviewBytes;
            if (bytes is null)
            {
                DrawText(context, "No byte data", HexTextBrush, new Point(LeftPadding, HeaderHeight + TopPadding));
                return;
            }

            int bytesPerRow = _owner.GetBytesPerRow();
            int offsetDigits = GetOffsetDigitCount(bytes);
            double offsetColumnWidth = offsetDigits * CharWidth;
            double hexColumnStart = LeftPadding + offsetColumnWidth + OffsetGap - _owner.HorizontalOffset;
            double asciiColumnStart = hexColumnStart + bytesPerRow * 3 * CharWidth + AsciiGap;

            DrawText(context, "Offset", HeaderTextBrush, new Point(LeftPadding, 7));
            DrawText(context, CreateHeader(bytesPerRow), HeaderTextBrush, new Point(hexColumnStart, 7));
            DrawText(context, "ASCII", HeaderTextBrush, new Point(asciiColumnStart, 7));

            if (bytes.Length == 0)
            {
                DrawText(context, "No byte data", HexTextBrush, new Point(LeftPadding, HeaderHeight + TopPadding));
                return;
            }

            int startRow = _owner.FirstVisibleRow;
            int endRow = Math.Min(_owner.RowCount, startRow + _owner.VisibleRowCount + 1);
            for (int rowIndex = startRow; rowIndex < endRow; rowIndex++)
            {
                int offset = rowIndex * bytesPerRow;
                int count = Math.Min(bytesPerRow, bytes.Length - offset);
                double rowY = HeaderHeight + TopPadding + ((rowIndex - startRow) * RowHeight);

                if (rowIndex % 2 == 1)
                {
                    context.DrawRectangle(AlternateRowBrush, null, new Rect(0, rowY, bounds.Width, RowHeight));
                }

                DrawText(context, offset.ToString($"X{offsetDigits}", CultureInfo.InvariantCulture), OffsetTextBrush, new Point(LeftPadding, rowY + 2));
                DrawText(context, CreateHexRow(bytes, offset, count), HexTextBrush, new Point(hexColumnStart, rowY + 2));
                DrawText(context, CreateAsciiRow(bytes, offset, count), AsciiTextBrush, new Point(asciiColumnStart, rowY + 2));
            }
        }
    }
}