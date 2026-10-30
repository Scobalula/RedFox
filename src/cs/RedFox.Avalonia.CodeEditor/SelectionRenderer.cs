using Avalonia;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;

namespace RedFox.Avalonia.CodeEditor;

/// <summary>
/// Draws the selection as VS Code does: one rounded shape running a character past each line, with whitespace marked inside it.
/// </summary>
internal sealed class SelectionRenderer(TextArea textArea) : IBackgroundRenderer
{
    private const double CornerRadius = 3.0;
    private const double ArrowHeadSize = 2.5;
    private const double SpaceDotRadius = 1.0;

    /// <summary>
    /// Gets or sets the selection colour.
    /// </summary>
    public IBrush? Brush { get; set; }

    /// <summary>
    /// Gets or sets the colour of the whitespace marks.
    /// </summary>
    public IBrush? WhitespaceBrush { get; set; }

    /// <inheritdoc/>
    public KnownLayer Layer => KnownLayer.Selection;

    /// <inheritdoc/>
    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        TextDocument? document = textView.Document;
        if (Brush is null || document is null || textArea.Selection.IsEmpty || !textView.VisualLinesValid)
        {
            return;
        }

        BackgroundGeometryBuilder builder = new() { AlignToWholePixels = true, CornerRadius = CornerRadius };

        foreach (SelectionSegment segment in textArea.Selection.Segments)
        {
            AddSegment(builder, textView, document, segment);
        }

        Geometry? geometry = builder.CreateGeometry();
        if (geometry is not null)
        {
            drawingContext.DrawGeometry(Brush, null, geometry);
        }

        foreach (SelectionSegment segment in textArea.Selection.Segments)
        {
            DrawWhitespace(textView, document, segment, drawingContext);
        }
    }

    private static void AddSegment(BackgroundGeometryBuilder builder, TextView textView, TextDocument document, SelectionSegment segment)
    {
        DocumentLine lastLine	= document.GetLineByOffset(segment.EndOffset);
        double hiddenFrom		= segment.EndOffset == lastLine.Offset ? textView.GetVisualTopByDocumentLine(lastLine.LineNumber) - textView.ScrollOffset.Y : double.PositiveInfinity;

        foreach (Rect rectangle in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
        {
            if (rectangle.Top + 1.0 < hiddenFrom)
            {
                builder.AddRectangle(textView, ExtendPastLine(textView, segment, rectangle));
            }
        }
    }

    private static Rect ExtendPastLine(TextView textView, SelectionSegment segment, Rect rectangle)
    {
        DocumentLine? line = textView.GetDocumentLineByVisualTop(rectangle.Top + textView.ScrollOffset.Y + 1.0);
        if (line is null || segment.EndOffset <= line.EndOffset)
        {
            return rectangle;
        }

        double lineEnd = textView.GetVisualPosition(new TextViewPosition(line.LineNumber, line.Length + 1), VisualYPosition.TextTop).X - textView.ScrollOffset.X;
        return new Rect(rectangle.Left, rectangle.Top, lineEnd + textView.WideSpaceWidth - rectangle.Left, rectangle.Height);
    }

    private void DrawWhitespace(TextView textView, TextDocument document, SelectionSegment segment, DrawingContext drawingContext)
    {
        if (WhitespaceBrush is null)
        {
            return;
        }

        Pen pen = new(WhitespaceBrush);

        foreach (VisualLine line in textView.VisualLines)
        {
            DocumentLine documentLine	= line.FirstDocumentLine;
            int start					= Math.Max(segment.StartOffset, documentLine.Offset);
            int end						= Math.Min(segment.EndOffset, documentLine.EndOffset);

            for (int offset = start; offset < end; offset++)
            {
                char character = document.GetCharAt(offset);

                if (character is ' ' or '\t')
                {
                    TextViewPosition position	= new(documentLine.LineNumber, offset - documentLine.Offset + 1);
                    Point point					= textView.GetVisualPosition(position, VisualYPosition.TextMiddle) - textView.ScrollOffset;
                    DrawMark(drawingContext, pen, point, textView.WideSpaceWidth, character == '\t');
                }
            }
        }
    }

    private void DrawMark(DrawingContext drawingContext, Pen pen, Point point, double width, bool isTab)
    {
        if (!isTab)
        {
            drawingContext.DrawEllipse(WhitespaceBrush, null, new Point(point.X + (width / 2.0), point.Y), SpaceDotRadius, SpaceDotRadius);
            return;
        }

        Point tip = new(point.X + width - 1.0, point.Y);

        drawingContext.DrawLine(pen, new Point(point.X + 1.0, point.Y), tip);
        drawingContext.DrawLine(pen, new Point(tip.X - ArrowHeadSize, tip.Y - ArrowHeadSize), tip);
        drawingContext.DrawLine(pen, new Point(tip.X - ArrowHeadSize, tip.Y + ArrowHeadSize), tip);
    }
}
