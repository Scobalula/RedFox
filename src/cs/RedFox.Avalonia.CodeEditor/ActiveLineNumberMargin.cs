using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;

namespace RedFox.Avalonia.CodeEditor;

/// <summary>
/// Draws line numbers with the caret's line brighter than the rest.
/// </summary>
internal sealed class ActiveLineNumberMargin : LineNumberMargin
{
    /// <summary>
    /// Gets or sets the line number colour.
    /// </summary>
    public IBrush? LineForeground { get; set; }

    /// <summary>
    /// Gets or sets the colour of the caret line's number.
    /// </summary>
    public IBrush? ActiveForeground { get; set; }

    /// <summary>
    /// Gets or sets the line that holds the caret.
    /// </summary>
    public int ActiveLine { get; set; }

    /// <inheritdoc/>
    public override void Render(DrawingContext drawingContext)
    {
        TextView? textView = TextView;
        if (textView is null || !textView.VisualLinesValid)
        {
            return;
        }

        Typeface typeface	= new(GetValue(TextBlock.FontFamilyProperty));
        double size			= GetValue(TextBlock.FontSizeProperty);

        foreach (VisualLine line in textView.VisualLines)
        {
            int number			= line.FirstDocumentLine.LineNumber;
            IBrush? brush		= number == ActiveLine ? ActiveForeground : LineForeground;
            FormattedText text	= new(number.ToString(CultureInfo.CurrentCulture), CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, size, brush);
            double top			= line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.TextTop) - textView.VerticalOffset;

            drawingContext.DrawText(text, new Point(Bounds.Width - text.Width, top));
        }
    }
}
