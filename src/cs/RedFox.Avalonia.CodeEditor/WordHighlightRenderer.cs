using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;

namespace RedFox.Avalonia.CodeEditor;

/// <summary>
/// Highlights the other occurrences of the selection, or of the word under the caret, in the visible lines.
/// </summary>
internal sealed class WordHighlightRenderer(TextArea textArea) : IBackgroundRenderer
{
    /// <summary>
    /// Gets or sets the highlight colour.
    /// </summary>
    public IBrush? Brush { get; set; }

    /// <inheritdoc/>
    public KnownLayer Layer => KnownLayer.Selection;

    /// <inheritdoc/>
    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        TextDocument? document = textView.Document;
        if (Brush is null || document is null || !textArea.IsKeyboardFocusWithin || !textView.VisualLinesValid)
        {
            return;
        }

        bool wholeWord	= textArea.Selection.IsEmpty;
        string term		= GetTerm(document, wholeWord);
        if (term.Length == 0)
        {
            return;
        }

        foreach (VisualLine line in textView.VisualLines)
        {
            DocumentLine documentLine = line.FirstDocumentLine;

            foreach (int column in WordOccurrences.Find(document.GetText(documentLine.Offset, documentLine.Length), term, wholeWord))
            {
                TextSegment segment = new()
                {
                    StartOffset	= documentLine.Offset + column,
                    Length		= term.Length,
                };

                foreach (Rect rectangle in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
                {
                    drawingContext.FillRectangle(Brush, rectangle);
                }
            }
        }
    }

    private string GetTerm(TextDocument document, bool useCaretWord)
    {
        if (!useCaretWord)
        {
            string selected = textArea.Selection.GetText();
            return string.IsNullOrWhiteSpace(selected) || selected.Contains('\n') ? string.Empty : selected;
        }

        DocumentLine line = document.GetLineByNumber(textArea.Caret.Line);
        return WordOccurrences.FindWord(document.GetText(line.Offset, line.Length), textArea.Caret.Column - 1);
    }
}
