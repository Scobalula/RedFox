using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;

namespace RedFox.Avalonia.CodeEditor;

/// <summary>
/// Outlines the bracket touching the caret and its partner.
/// </summary>
internal sealed class BracketMatchRenderer(TextArea textArea) : IBackgroundRenderer
{
    /// <summary>
    /// Gets or sets the pairs of the current document.
    /// </summary>
    public IReadOnlyDictionary<int, int> Pairs { get; set; } = new Dictionary<int, int>();

    /// <summary>
    /// Gets or sets the fill colour.
    /// </summary>
    public IBrush? Brush { get; set; }

    /// <summary>
    /// Gets or sets the outline pen.
    /// </summary>
    public IPen? Pen { get; set; }

    /// <inheritdoc/>
    public KnownLayer Layer => KnownLayer.Selection;

    /// <inheritdoc/>
    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        TextDocument? document = textView.Document;
        if (document is null || !textView.VisualLinesValid || !BracketPairs.TryFind(Pairs, textArea.Caret.Offset, out int bracket, out int partner))
        {
            return;
        }

        DrawBox(textView, document, bracket, drawingContext);
        DrawBox(textView, document, partner, drawingContext);
    }

    private void DrawBox(TextView textView, TextDocument document, int offset, DrawingContext drawingContext)
    {
        if (offset >= document.TextLength)
        {
            return;
        }

        TextSegment segment = new()
        {
            StartOffset	= offset,
            Length		= 1,
        };

        foreach (Rect rectangle in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
        {
            double left	= Math.Round(rectangle.Left) + 0.5;
            double top	= Math.Round(rectangle.Top) + 0.5;
            drawingContext.DrawRectangle(Brush, Pen, new Rect(left, top, Math.Round(rectangle.Width) - 1.0, Math.Round(rectangle.Height) - 1.0));
        }
    }
}
