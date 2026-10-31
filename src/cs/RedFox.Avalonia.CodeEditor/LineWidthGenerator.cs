using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace RedFox.Avalonia.CodeEditor;

/// <summary>
/// Pads each visible line to the document's widest line, so the horizontal scroll range covers the whole document and not only the
/// lines on screen. The padding has no length in the document.
/// </summary>
internal sealed class LineWidthGenerator : VisualLineElementGenerator
{
    private const char Filler = ' ';

    /// <summary>
    /// Gets or sets the columns every line is padded to.
    /// </summary>
    public int Columns { get; set; }

    /// <summary>
    /// Gets or sets the columns one tab occupies.
    /// </summary>
    public int TabSize { get; set; } = 4;

    /// <inheritdoc/>
    public override int GetFirstInterestedOffset(int startOffset)
    {
        int lineEnd = CurrentContext.VisualLine.LastDocumentLine.EndOffset;
        return startOffset <= lineEnd ? lineEnd : -1;
    }

    /// <inheritdoc/>
    public override VisualLineElement? ConstructElement(int offset)
    {
        DocumentLine line	= CurrentContext.VisualLine.LastDocumentLine;
        int padding			= Columns - IndentGuides.GetColumnCount(CurrentContext.Document.GetText(line.Offset, line.Length), TabSize);

        return padding > 0 ? new FormattedTextElement(new string(Filler, padding), 0) : null;
    }
}
