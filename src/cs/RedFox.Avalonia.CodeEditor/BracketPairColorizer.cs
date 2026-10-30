using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace RedFox.Avalonia.CodeEditor;

/// <summary>
/// Colours brackets by nesting depth.
/// </summary>
internal sealed class BracketPairColorizer : DocumentColorizingTransformer
{
    private IReadOnlyList<BracketToken> _brackets = [];
    private IReadOnlyList<IBrush> _brushes = [];

    /// <summary>
    /// Replaces the brackets and the colours they cycle through.
    /// </summary>
    /// <param name="brackets">The brackets, ordered by offset.</param>
    /// <param name="brushes">The depth colours.</param>
    public void Update(IReadOnlyList<BracketToken> brackets, IReadOnlyList<IBrush> brushes)
    {
        _brackets = brackets;
        _brushes = brushes;
    }

    /// <inheritdoc/>
    protected override void ColorizeLine(DocumentLine line)
    {
        if (_brushes.Count == 0)
        {
            return;
        }

        for (int index = FindFirst(line.Offset); index < _brackets.Count && _brackets[index].Offset < line.EndOffset; index++)
        {
            BracketToken bracket = _brackets[index];
            IBrush brush = _brushes[bracket.Depth % _brushes.Count];
            ChangeLinePart(bracket.Offset, bracket.Offset + 1, element => element.TextRunProperties.SetForegroundBrush(brush));
        }
    }

    private int FindFirst(int offset)
    {
        int low = 0;
        int high = _brackets.Count;

        while (low < high)
        {
            int middle = (low + high) / 2;

            if (_brackets[middle].Offset < offset)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }
}
