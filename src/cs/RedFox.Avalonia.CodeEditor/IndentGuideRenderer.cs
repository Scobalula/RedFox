using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace RedFox.Avalonia.CodeEditor;

/// <summary>
/// Draws a vertical guide at each indent level of the visible lines.
/// </summary>
internal sealed class IndentGuideRenderer : IBackgroundRenderer
{
    /// <summary>
    /// Gets or sets the guide pen.
    /// </summary>
    public IPen? Pen { get; set; }

    /// <summary>
    /// Gets or sets the columns one level occupies.
    /// </summary>
    public int TabSize { get; set; } = 4;

    /// <inheritdoc/>
    public KnownLayer Layer => KnownLayer.Background;

    /// <inheritdoc/>
    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        TextDocument? document = textView.Document;
        if (Pen is null || document is null || !textView.VisualLinesValid)
        {
            return;
        }

        double width = textView.WideSpaceWidth * TabSize;

        foreach (VisualLine line in textView.VisualLines)
        {
            int level = GetLevel(document, line.FirstDocumentLine);
            double top = line.VisualTop - textView.ScrollOffset.Y;

            for (int guide = 0; guide < level; guide++)
            {
                double x = Math.Round((guide * width) - textView.ScrollOffset.X) + 0.5;
                drawingContext.DrawLine(Pen, new Point(x, top), new Point(x, top + line.Height));
            }
        }
    }

    // A blank line carries the guides of the code around it, so they run unbroken through a block.
    private int GetLevel(TextDocument document, DocumentLine line)
    {
        int level = Measure(document, line);
        if (level >= 0)
        {
            return level;
        }

        return Math.Min(FindLevel(document, line, static next => next.PreviousLine), FindLevel(document, line, static next => next.NextLine));
    }

    private int FindLevel(TextDocument document, DocumentLine line, Func<DocumentLine, DocumentLine?> step)
    {
        for (DocumentLine? next = step(line); next is not null; next = step(next))
        {
            int level = Measure(document, next);
            if (level >= 0)
            {
                return level;
            }
        }

        return 0;
    }

    private int Measure(TextDocument document, DocumentLine line)
    {
        return IndentGuides.GetLevel(document.GetText(line.Offset, line.Length), TabSize);
    }
}
