using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;

namespace RedFox.Avalonia.CodeEditor;

/// <summary>
/// Draws a vertical guide at each indent level of the visible lines, over the selection and with the caret's block brighter.
/// The outermost level has none, as the line beside the line numbers already marks that edge.
/// </summary>
internal sealed class IndentGuideRenderer(TextArea textArea) : IBackgroundRenderer
{
    /// <summary>
    /// Gets or sets the guide pen.
    /// </summary>
    public IPen? Pen { get; set; }

    /// <summary>
    /// Gets or sets the pen of the guide around the caret.
    /// </summary>
    public IPen? ActivePen { get; set; }

    /// <summary>
    /// Gets or sets the columns one level occupies.
    /// </summary>
    public int TabSize { get; set; } = 4;

    /// <inheritdoc/>
    public KnownLayer Layer => KnownLayer.Text;

    /// <inheritdoc/>
    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        TextDocument? document = textView.Document;
        if (Pen is null || document is null || !textView.VisualLinesValid)
        {
            return;
        }

        double width			= textView.WideSpaceWidth * TabSize;
        DocumentLine caretLine	= document.GetLineByNumber(textArea.Caret.Line);
        int activeGuide			= GetActiveGuide(document, caretLine);
        int firstActiveLine		= FindBlockEnd(document, caretLine, activeGuide, static next => next.PreviousLine);
        int lastActiveLine		= FindBlockEnd(document, caretLine, activeGuide, static next => next.NextLine);

        foreach (VisualLine line in textView.VisualLines)
        {
            int number	= line.FirstDocumentLine.LineNumber;
            int level	= GetLevel(document, line.FirstDocumentLine);
            double top	= line.VisualTop - textView.ScrollOffset.Y;

            for (int guide = 1; guide < level; guide++)
            {
                bool isActive	= guide == activeGuide && number >= firstActiveLine && number <= lastActiveLine;
                double x		= Math.Round((guide * width) - textView.ScrollOffset.X) + 0.5;
                drawingContext.DrawLine(isActive ? ActivePen ?? Pen : Pen, new Point(x, top), new Point(x, top + line.Height));
            }
        }
    }

    private int GetActiveGuide(TextDocument document, DocumentLine caretLine)
    {
        int above	= FindLevel(document, caretLine, static next => next.PreviousLine);
        int below	= FindLevel(document, caretLine, static next => next.NextLine);

        return Math.Max(GetLevel(document, caretLine), Math.Max(above, below)) - 1;
    }

    private int FindBlockEnd(TextDocument document, DocumentLine caretLine, int guide, Func<DocumentLine, DocumentLine?> step)
    {
        DocumentLine end = caretLine;

        if (guide >= 0)
        {
            for (DocumentLine? next = step(end); next is not null && GetLevel(document, next) > guide; next = step(end))
            {
                end = next;
            }
        }

        return end.LineNumber;
    }

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
