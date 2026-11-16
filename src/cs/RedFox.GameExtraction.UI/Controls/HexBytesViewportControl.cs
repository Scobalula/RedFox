using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace RedFox.GameExtraction.UI.Controls;

internal sealed class HexBytesViewportControl(HexBytesPreviewControl owner) : Control
{
    private readonly HexBytesPreviewControl _owner = owner ?? throw new ArgumentNullException(nameof(owner));

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        Rect bounds					= Bounds;
        IBrush textBrush			= ThemeBrush("ThemeForegroundBrush");
        IBrush mutedTextBrush		= ThemeBrush("ThemeMutedForegroundBrush");
        IBrush[] groupBrushes		= [.. HexBytesPreviewControl.ByteGroupBrushKeys.Select(ThemeBrush)];
        context.DrawRectangle(ThemeBrush("ThemeBackgroundBrush"), null, bounds);
        context.DrawRectangle(ThemeBrush("ThemeHeaderBrush"), null, new Rect(0, 0, bounds.Width, HexBytesPreviewControl.HeaderHeight));
        context.DrawLine(new Pen(ThemeBrush("ThemeBorderBrush"), 1), new Point(0, HexBytesPreviewControl.HeaderHeight - 0.5), new Point(bounds.Width, HexBytesPreviewControl.HeaderHeight - 0.5));

        byte[]? bytes = _owner.PreviewBytes;
        if (bytes is null)
        {
            HexBytesPreviewControl.DrawText(context, "No byte data", textBrush, new Point(HexBytesPreviewControl.LeftPadding, HexBytesPreviewControl.HeaderHeight + HexBytesPreviewControl.TopPadding));
            return;
        }

        int bytesPerRow = _owner.GetBytesPerRow();
        int offsetDigits = HexBytesPreviewControl.GetOffsetDigitCount(bytes);
        double offsetColumnWidth = offsetDigits * HexBytesPreviewControl.CharWidth;
        double hexColumnStart = HexBytesPreviewControl.LeftPadding + offsetColumnWidth + HexBytesPreviewControl.OffsetGap - _owner.HorizontalOffset;
        int groupSeparators = HexBytesPreviewControl.GetByteGroupSeparatorCount(bytesPerRow);
        double hexColumnWidth = (bytesPerRow * 3 + groupSeparators) * HexBytesPreviewControl.CharWidth;
        double asciiColumnStart = hexColumnStart + hexColumnWidth + HexBytesPreviewControl.AsciiGap;

        HexBytesPreviewControl.DrawText(context, "Offset", mutedTextBrush, new Point(HexBytesPreviewControl.LeftPadding, 7));
        HexBytesPreviewControl.DrawGroupedText(context, HexBytesPreviewControl.CreateHeader(bytesPerRow), bytesPerRow, isHex: true, new Point(hexColumnStart, 7), textBrush, groupBrushes);
        HexBytesPreviewControl.DrawText(context, "ASCII", mutedTextBrush, new Point(asciiColumnStart, 7));

        if (bytes.Length == 0)
        {
            HexBytesPreviewControl.DrawText(context, "No byte data", textBrush, new Point(HexBytesPreviewControl.LeftPadding, HexBytesPreviewControl.HeaderHeight + HexBytesPreviewControl.TopPadding));
            return;
        }

        int startRow = _owner.FirstVisibleRow;
        int endRow = Math.Min(_owner.RowCount, startRow + _owner.VisibleRowCount + 1);
        for (int rowIndex = startRow; rowIndex < endRow; rowIndex++)
        {
            int offset = rowIndex * bytesPerRow;
            int count = Math.Min(bytesPerRow, bytes.Length - offset);
            double rowY = HexBytesPreviewControl.HeaderHeight + HexBytesPreviewControl.TopPadding + ((rowIndex - startRow) * HexBytesPreviewControl.RowHeight);

            if (rowIndex % 2 == 1)
            {
                context.DrawRectangle(ThemeBrush("ThemeCardBrush"), null, new Rect(0, rowY, bounds.Width, HexBytesPreviewControl.RowHeight));
            }

            HexBytesPreviewControl.DrawText(context, offset.ToString($"X{offsetDigits}", CultureInfo.InvariantCulture), mutedTextBrush, new Point(HexBytesPreviewControl.LeftPadding, rowY + 2));
            HexBytesPreviewControl.DrawGroupedText(context, HexBytesPreviewControl.CreateHexRow(bytes, offset, count), count, isHex: true, new Point(hexColumnStart, rowY + 2), textBrush, groupBrushes);
            HexBytesPreviewControl.DrawGroupedText(context, HexBytesPreviewControl.CreateAsciiRow(bytes, offset, count), count, isHex: false, new Point(asciiColumnStart, rowY + 2), textBrush, groupBrushes);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ThemeVariantScope.ActualThemeVariantProperty)
        {
            InvalidateVisual();
        }
    }

    private IBrush ThemeBrush(string key)
    {
        return this.TryFindResource(key, ActualThemeVariant, out object? value) && value is IBrush brush ? brush : Brushes.Transparent;
    }
}
