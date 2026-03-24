using System.Text;

namespace RedFox.Graphics3D.KaydaraFbx;

/// <summary>
/// Provides a lightweight tokenizer for FBX ASCII text.
/// </summary>
public sealed class FbxAsciiTokenizer
{
    private readonly string _text;
    private readonly int _end;
    private int _position;

    /// <summary>
    /// Initializes a new tokenizer over the given FBX ASCII text.
    /// </summary>
    /// <param name="text">The full ASCII FBX document text.</param>
    public FbxAsciiTokenizer(string text)
    {
        _text = text;
        _position = 0;
        _end = text.Length;
    }

    /// <summary>
    /// Initializes a new tokenizer over a bounded slice of FBX ASCII text.
    /// </summary>
    /// <param name="text">The full ASCII FBX document text.</param>
    /// <param name="start">The inclusive start offset.</param>
    /// <param name="length">The slice length.</param>
    public FbxAsciiTokenizer(string text, int start, int length)
    {
        _text = text;
        _position = start;
        _end = start + length;
    }

    /// <summary>
    /// Gets a value indicating whether the tokenizer has consumed the full input.
    /// </summary>
    public bool IsEnd => _position >= _end;

    /// <summary>
    /// Gets the backing ASCII text.
    /// </summary>
    public string Text => _text;

    /// <summary>
    /// Gets the current character offset within the source text.
    /// </summary>
    public int Position => _position;

    /// <summary>
    /// Gets a value indicating whether the last successful <see cref="TryConsume(char)"/> call opened a block.
    /// </summary>
    public bool LastOpenedBlock { get; private set; }

    /// <summary>
    /// Peeks the current character without advancing the tokenizer.
    /// </summary>
    /// <returns>The current character, or <c>\0</c> when the tokenizer is at the end.</returns>
    public char PeekChar()
    {
        if (IsEnd)
        {
            return '\0';
        }

        return _text[_position];
    }

    /// <summary>
    /// Advances the tokenizer by one character when input remains.
    /// </summary>
    public void AdvanceOne()
    {
        if (!IsEnd)
        {
            _position++;
        }
    }

    /// <summary>
    /// Tries to consume the specified character after skipping comments and whitespace.
    /// </summary>
    /// <param name="expected">The character to consume.</param>
    /// <returns><see langword="true"/> when the character was consumed; otherwise <see langword="false"/>.</returns>
    public bool TryConsume(char expected)
    {
        TryConsumeCommentOrWhitespace();
        if (PeekChar() != expected)
        {
            if (expected == '{')
            {
                LastOpenedBlock = false;
            }

            return false;
        }

        _position++;
        LastOpenedBlock = expected == '{';
        return true;
    }

    /// <summary>
    /// Consumes any contiguous line-ending characters.
    /// </summary>
    public void ConsumeLineEndings()
    {
        while (!IsEnd)
        {
            char c = PeekChar();
            if (c != '\n' && c != '\r')
            {
                break;
            }

            _position++;
        }
    }

    /// <summary>
    /// Consumes any leading ASCII FBX comments and whitespace.
    /// </summary>
    /// <returns><see langword="true"/> when any characters were consumed; otherwise <see langword="false"/>.</returns>
    public bool TryConsumeCommentOrWhitespace()
    {
        bool consumed = false;
        while (!IsEnd)
        {
            char c = PeekChar();
            if (char.IsWhiteSpace(c))
            {
                consumed = true;
                _position++;
                continue;
            }

            if (c == ';')
            {
                consumed = true;
                while (!IsEnd)
                {
                    char commentChar = PeekChar();
                    _position++;
                    if (commentChar == '\n')
                    {
                        break;
                    }
                }

                continue;
            }

            break;
        }

        return consumed;
    }

    /// <summary>
    /// Reads an unquoted FBX identifier token.
    /// </summary>
    /// <returns>The identifier text, or <see langword="null"/> when no identifier is present.</returns>
    public string? ReadIdentifier()
    {
        return TryReadIdentifierRange(out int start, out int length) ? _text.Substring(start, length) : null;
    }

    /// <summary>
    /// Tries to read an unquoted FBX identifier token range without allocating a new string.
    /// </summary>
    /// <param name="start">Receives the inclusive start offset.</param>
    /// <param name="length">Receives the token length.</param>
    /// <returns><see langword="true"/> when an identifier token was read.</returns>
    public bool TryReadIdentifierRange(out int start, out int length)
    {
        TryConsumeCommentOrWhitespace();
        if (IsEnd)
        {
            start = 0;
            length = 0;
            return false;
        }

        start = _position;
        char first = PeekChar();
        if (!IsIdentifierStartChar(first))
        {
            length = 0;
            return false;
        }

        _position++;
        while (!IsEnd)
        {
            char c = PeekChar();
            if (!IsIdentifierChar(c))
            {
                break;
            }

            _position++;
        }

        length = _position - start;
        return true;
    }

    /// <summary>
    /// Reads a numeric token used by FBX ASCII scalar and array encodings.
    /// </summary>
    /// <returns>The numeric token text, or <see langword="null"/> when no numeric token is present.</returns>
    public string? ReadNumberToken()
    {
        return TryReadNumberRange(out int start, out int length) ? _text.Substring(start, length) : null;
    }

    /// <summary>
    /// Tries to read a numeric token range without allocating a new string.
    /// </summary>
    /// <param name="start">Receives the inclusive start offset.</param>
    /// <param name="length">Receives the token length.</param>
    /// <returns><see langword="true"/> when a numeric token was read.</returns>
    public bool TryReadNumberRange(out int start, out int length)
    {
        TryConsumeCommentOrWhitespace();
        if (IsEnd)
        {
            start = 0;
            length = 0;
            return false;
        }

        start = _position;
        char first = PeekChar();
        if (!IsNumberStartChar(first))
        {
            length = 0;
            return false;
        }

        _position++;
        while (!IsEnd)
        {
            char c = PeekChar();
            if (!IsNumberChar(c))
            {
                break;
            }

            _position++;
        }

        length = _position - start;
        return true;
    }

    /// <summary>
    /// Reads the next scalar FBX value token.
    /// </summary>
    /// <returns>The token text without surrounding quotes.</returns>
    public string ReadValueToken()
    {
        TryConsumeCommentOrWhitespace();

        if (PeekChar() == '"')
        {
            return ReadQuotedString();
        }

        int start = _position;
        while (!IsEnd)
        {
            char c = PeekChar();
            if (c == ',' || c == '{' || c == '}' || c == '\n' || c == '\r')
            {
                break;
            }

            _position++;
        }

        return _text[start.._position].Trim();
    }

    /// <summary>
    /// Reads a quoted FBX string token.
    /// </summary>
    /// <returns>The unescaped string value.</returns>
    public string ReadQuotedString()
    {
        if (!TryConsume('"'))
        {
            throw new InvalidDataException("Expected opening quote for FBX string.");
        }

        StringBuilder result = new();
        while (!IsEnd)
        {
            char c = PeekChar();
            _position++;

            if (c == '"')
            {
                break;
            }

            if (c == '\\' && !IsEnd)
            {
                char escaped = PeekChar();
                _position++;
                result.Append(escaped);
                continue;
            }

            result.Append(c);
        }

        return result.ToString();
    }

    /// <summary>
    /// Reads the contents of the current block until the matching closing brace.
    /// </summary>
    /// <returns>The raw block contents, excluding the closing brace.</returns>
    public string ReadBlockContents()
    {
        (int blockStart, int blockEnd) = ScanBlock();
        return _text[blockStart..blockEnd];
    }

    /// <summary>
    /// Reads the source range covered by the current block until the matching closing brace.
    /// </summary>
    /// <returns>The start and exclusive end offsets of the block contents.</returns>
    public (int Start, int End) ReadBlockRange() => ScanBlock();

    /// <summary>
    /// Scans from the current position through a brace-delimited block, tracking nested braces,
    /// comments, and quoted strings. On return the tokenizer is positioned immediately past the
    /// closing brace. The returned range covers the interior content only.
    /// </summary>
    /// <returns>The inclusive-start and exclusive-end offsets of the block interior.</returns>
    private (int Start, int End) ScanBlock()
    {
        int start = _position;
        int depth = 1;
        bool inString = false;
        bool inComment = false;

        while (!IsEnd)
        {
            char current = PeekChar();

            if (inComment)
            {
                _position++;
                if (current == '\n')
                {
                    inComment = false;
                }

                continue;
            }

            if (inString)
            {
                _position++;
                if (current == '\\' && !IsEnd)
                {
                    _position++;
                    continue;
                }

                if (current == '"')
                {
                    inString = false;
                }

                continue;
            }

            switch (current)
            {
                case ';':
                    inComment = true;
                    _position++;
                    continue;
                case '"':
                    inString = true;
                    _position++;
                    continue;
                case '{':
                    depth++;
                    _position++;
                    continue;
                case '}':
                    depth--;
                    if (depth == 0)
                    {
                        int end = _position;
                        _position++;
                        return (start, end);
                    }

                    _position++;
                    continue;
                default:
                    _position++;
                    continue;
            }
        }

        return (start, _position);
    }

    /// <summary>
    /// Determines whether the specified character is a valid first character for an FBX ASCII identifier.
    /// </summary>
    /// <param name="c">The character to test.</param>
    /// <returns><see langword="true"/> when the character can start an identifier.</returns>
    public static bool IsIdentifierStartChar(char c) => char.IsLetter(c) || c == '_' || c == '|';

    /// <summary>
    /// Determines whether the specified character is a valid continuation character for an FBX ASCII identifier.
    /// </summary>
    /// <param name="c">The character to test.</param>
    /// <returns><see langword="true"/> when the character can continue an identifier.</returns>
    public static bool IsIdentifierChar(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '|';

    /// <summary>
    /// Determines whether the specified character is a valid first character for an FBX ASCII numeric token.
    /// </summary>
    /// <param name="c">The character to test.</param>
    /// <returns><see langword="true"/> when the character can start a numeric token.</returns>
    public static bool IsNumberStartChar(char c) => char.IsDigit(c) || c == '-' || c == '+';

    /// <summary>
    /// Determines whether the specified character is a valid continuation character for an FBX ASCII numeric token.
    /// </summary>
    /// <param name="c">The character to test.</param>
    /// <returns><see langword="true"/> when the character can continue a numeric token.</returns>
    public static bool IsNumberChar(char c) => char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '+' || c == 'e' || c == 'E';
}