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

        Rect bounds = Bounds;
        context.DrawRectangle(HexBytesPreviewControl.BackgroundBrush, null, bounds);
        context.DrawRectangle(HexBytesPreviewControl.HeaderBackgroundBrush, null, new Rect(0, 0, bounds.Width, HexBytesPreviewControl.HeaderHeight));
        context.DrawLine(new Pen(HexBytesPreviewControl.BorderStrokeBrush, 1), new Point(0, HexBytesPreviewControl.HeaderHeight - 0.5), new Point(bounds.Width, HexBytesPreviewControl.HeaderHeight - 0.5));

        byte[]? bytes = _owner.PreviewBytes;
        if (bytes is null)
        {
            HexBytesPreviewControl.DrawText(context, "No byte data", HexBytesPreviewControl.HexTextBrush, new Point(HexBytesPreviewControl.LeftPadding, HexBytesPreviewControl.HeaderHeight + HexBytesPreviewControl.TopPadding));
            return;
        }

        int bytesPerRow = _owner.GetBytesPerRow();
        int offsetDigits = HexBytesPreviewControl.GetOffsetDigitCount(bytes);
        double offsetColumnWidth = offsetDigits * HexBytesPreviewControl.CharWidth;
        double hexColumnStart = HexBytesPreviewControl.LeftPadding + offsetColumnWidth + HexBytesPreviewControl.OffsetGap - _owner.HorizontalOffset;
        double asciiColumnStart = hexColumnStart + bytesPerRow * 3 * HexBytesPreviewControl.CharWidth + HexBytesPreviewControl.AsciiGap;

        HexBytesPreviewControl.DrawText(context, "Offset", HexBytesPreviewControl.HeaderTextBrush, new Point(HexBytesPreviewControl.LeftPadding, 7));
        HexBytesPreviewControl.DrawText(context, HexBytesPreviewControl.CreateHeader(bytesPerRow), HexBytesPreviewControl.HeaderTextBrush, new Point(hexColumnStart, 7));
        HexBytesPreviewControl.DrawText(context, "ASCII", HexBytesPreviewControl.HeaderTextBrush, new Point(asciiColumnStart, 7));

        if (bytes.Length == 0)
        {
            HexBytesPreviewControl.DrawText(context, "No byte data", HexBytesPreviewControl.HexTextBrush, new Point(HexBytesPreviewControl.LeftPadding, HexBytesPreviewControl.HeaderHeight + HexBytesPreviewControl.TopPadding));
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
                context.DrawRectangle(HexBytesPreviewControl.AlternateRowBrush, null, new Rect(0, rowY, bounds.Width, HexBytesPreviewControl.RowHeight));

            HexBytesPreviewControl.DrawText(context, offset.ToString($"X{offsetDigits}", CultureInfo.InvariantCulture), HexBytesPreviewControl.OffsetTextBrush, new Point(HexBytesPreviewControl.LeftPadding, rowY + 2));
            HexBytesPreviewControl.DrawText(context, HexBytesPreviewControl.CreateHexRow(bytes, offset, count), HexBytesPreviewControl.HexTextBrush, new Point(hexColumnStart, rowY + 2));
            HexBytesPreviewControl.DrawText(context, HexBytesPreviewControl.CreateAsciiRow(bytes, offset, count), HexBytesPreviewControl.AsciiTextBrush, new Point(asciiColumnStart, rowY + 2));
        }
    }
}
