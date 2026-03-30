// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using System.Globalization;
using System.Runtime.CompilerServices;

namespace RedFox.IO;

/// <summary>
/// Provides a high-performance, stack-only tokenizer for structured text formats.
/// <para>
/// The reader operates directly on a <see cref="ReadOnlySpan{T}"/> of characters, advancing
/// an internal cursor as tokens are consumed.  It handles whitespace skipping, line-comment
/// stripping (<c>//</c>), integer and floating-point parsing, quoted-string extraction, and
/// single-character expectations — all without heap allocations.
/// </para>
/// <para>
/// Typical usage is to read an entire text file into a <see cref="string"/>, obtain a span
/// via <see cref="MemoryExtensions.AsSpan(string)"/>, and construct the reader on the stack:
/// <code>
/// var reader = new TextTokenReader(fileText.AsSpan());
/// while (reader.TryReadToken(out var token)) { /* process token */ }
/// </code>
/// </para>
/// </summary>
/// <param name="text">The character span to tokenize.</param>
public ref struct TextTokenReader(ReadOnlySpan<char> text)
{
    private ReadOnlySpan<char> _remaining = text;

    /// <summary>
    /// Gets the unconsumed portion of the source span.
    /// </summary>
    public readonly ReadOnlySpan<char> Remaining => _remaining;

    /// <summary>
    /// Gets a value indicating whether all input has been consumed.
    /// </summary>
    public readonly bool IsEmpty => _remaining.IsEmpty;

    // ------------------------------------------------------------------
    // Whitespace and comment handling
    // ------------------------------------------------------------------

    /// <summary>
    /// Advances past any whitespace characters (spaces, tabs, carriage returns, line feeds).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SkipWhitespace()
    {
        _remaining = _remaining.TrimStart();
    }

    /// <summary>
    /// Advances past whitespace and <c>//</c> line comments repeatedly until a non-comment,
    /// non-whitespace character is reached or the input is exhausted.
    /// </summary>
    public void SkipWhitespaceAndComments()
    {
        while (true)
        {
            _remaining = _remaining.TrimStart();
            if (_remaining.IsEmpty) return;

            if (_remaining.Length >= 2 && _remaining[0] == '/' && _remaining[1] == '/')
            {
                int nl = _remaining.IndexOfAny('\r', '\n');
                _remaining = nl < 0 ? [] : _remaining[nl..].TrimStart();
                continue;
            }

            break;
        }
    }

    /// <summary>
    /// Advances the cursor past all characters on the current line, positioning immediately
    /// after the next line-feed or at end-of-input.
    /// </summary>
    public void SkipRestOfLine()
    {
        int nl = _remaining.IndexOf('\n');
        _remaining = nl < 0 ? [] : _remaining[(nl + 1)..];
    }

    // ------------------------------------------------------------------
    // Token reading
    // ------------------------------------------------------------------

    /// <summary>
    /// Reads the next whitespace-delimited token, skipping leading whitespace and line comments.
    /// </summary>
    /// <param name="token">
    /// When this method returns <see langword="true"/>, contains the token span.
    /// </param>
    /// <returns><see langword="true"/> if a token was read; <see langword="false"/> at end-of-input.</returns>
    public bool TryReadToken(out ReadOnlySpan<char> token)
    {
        SkipWhitespaceAndComments();

        if (_remaining.IsEmpty)
        {
            token = [];
            return false;
        }

        int end = 0;
        while (end < _remaining.Length && !char.IsWhiteSpace(_remaining[end]))
            end++;

        token = _remaining[..end];
        _remaining = _remaining[end..];
        return true;
    }

    /// <summary>
    /// Reads the next token and parses it as a 32-bit signed integer using invariant culture.
    /// </summary>
    /// <param name="value">The parsed integer value when successful.</param>
    /// <returns>
    /// <see langword="true"/> when an integer token is parsed; otherwise <see langword="false"/>.
    /// </returns>
    public bool TryReadInt(out int value)
    {
        SkipWhitespaceAndComments();

        if (_remaining.IsEmpty)
        {
            value = 0;
            return false;
        }

        int end = 0;
        if (end < _remaining.Length && (_remaining[end] == '-' || _remaining[end] == '+'))
            end++;
        while (end < _remaining.Length && char.IsAsciiDigit(_remaining[end]))
            end++;

        if (end == 0 || (end == 1 && !char.IsAsciiDigit(_remaining[0])))
        {
            value = 0;
            return false;
        }

        bool ok = int.TryParse(_remaining[..end], NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        if (ok) _remaining = _remaining[end..];
        return ok;
    }

    /// <summary>
    /// Reads the next token and parses it as a single-precision floating-point value using
    /// invariant culture.
    /// </summary>
    /// <param name="value">The parsed float value when successful.</param>
    /// <returns>
    /// <see langword="true"/> when a float token is parsed; otherwise <see langword="false"/>.
    /// </returns>
    public bool TryReadFloat(out float value)
    {
        SkipWhitespaceAndComments();

        if (_remaining.IsEmpty)
        {
            value = 0;
            return false;
        }

        int end = 0;
        if (end < _remaining.Length && (_remaining[end] == '-' || _remaining[end] == '+'))
            end++;
        while (end < _remaining.Length && (char.IsAsciiDigit(_remaining[end]) || _remaining[end] == '.' || _remaining[end] == 'e' || _remaining[end] == 'E'))
        {
            if ((_remaining[end] == 'e' || _remaining[end] == 'E') && end + 1 < _remaining.Length && (_remaining[end + 1] == '+' || _remaining[end + 1] == '-'))
                end++;
            end++;
        }

        if (end == 0 || (end == 1 && !char.IsAsciiDigit(_remaining[0])))
        {
            value = 0;
            return false;
        }

        bool ok = float.TryParse(_remaining[..end], NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        if (ok) _remaining = _remaining[end..];
        return ok;
    }

    /// <summary>
    /// Reads a double-quote delimited string token.  The leading and trailing quote characters
    /// are consumed but not included in the returned span.
    /// </summary>
    /// <param name="value">
    /// When this method returns <see langword="true"/>, contains the content between the quotes.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when a valid quoted string is found; otherwise <see langword="false"/>.
    /// </returns>
    public bool TryReadQuotedString(out ReadOnlySpan<char> value)
    {
        SkipWhitespaceAndComments();

        if (_remaining.IsEmpty || _remaining[0] != '"')
        {
            value = [];
            return false;
        }

        int close = _remaining[1..].IndexOf('"');
        if (close < 0)
        {
            value = [];
            return false;
        }

        value = _remaining[1..(close + 1)];
        _remaining = _remaining[(close + 2)..];
        return true;
    }

    /// <summary>
    /// Skips leading whitespace and comments, then expects and consumes the specified character.
    /// </summary>
    /// <param name="expected">The character to expect.</param>
    /// <returns>
    /// <see langword="true"/> when the character is found and consumed; otherwise <see langword="false"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryExpect(char expected)
    {
        SkipWhitespaceAndComments();

        if (_remaining.IsEmpty || _remaining[0] != expected)
            return false;

        _remaining = _remaining[1..];
        return true;
    }

    /// <summary>
    /// Skips leading whitespace and comments, then checks whether the remaining input begins
    /// with the specified keyword followed by whitespace or end-of-input.
    /// The keyword is consumed if matched.
    /// </summary>
    /// <param name="keyword">The keyword to match.</param>
    /// <returns><see langword="true"/> when the keyword is matched and consumed.</returns>
    public bool TryExpectToken(ReadOnlySpan<char> keyword)
    {
        SkipWhitespaceAndComments();

        if (_remaining.Length < keyword.Length)
            return false;

        if (!_remaining.StartsWith(keyword))
            return false;

        if (_remaining.Length > keyword.Length && !char.IsWhiteSpace(_remaining[keyword.Length]))
            return false;

        _remaining = _remaining[keyword.Length..];
        return true;
    }

    /// <summary>
    /// Reads the next token and compares it to the specified keyword.
    /// If the token matches, returns <see langword="true"/>; otherwise the cursor is NOT rewound.
    /// </summary>
    /// <param name="keyword">The keyword to match.</param>
    /// <returns><see langword="true"/> when the next token matches the keyword.</returns>
    public bool TryMatchToken(ReadOnlySpan<char> keyword)
    {
        if (!TryReadToken(out var token))
            return false;
        return token.SequenceEqual(keyword);
    }

    /// <summary>
    /// Reads the next token and parses it as a 32-bit signed integer.
    /// If parsing fails, the cursor advances past the token but <paramref name="value"/> is set to zero.
    /// </summary>
    /// <param name="value">The parsed integer value.</param>
    /// <returns>
    /// <see langword="true"/> when the token is a valid integer; otherwise <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Unlike <see cref="TryReadInt"/>, this method always consumes a token regardless of whether
    /// parsing succeeds.  Prefer <see cref="TryReadInt"/> when the caller may need to fall back.
    /// </remarks>
    public bool ReadInt(out int value)
    {
        if (!TryReadToken(out var token))
        {
            value = 0;
            return false;
        }

        return int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// Reads the next token and parses it as a single-precision float.
    /// If parsing fails, the cursor advances past the token but <paramref name="value"/> is set to zero.
    /// </summary>
    /// <param name="value">The parsed float value.</param>
    /// <returns>
    /// <see langword="true"/> when the token is a valid float; otherwise <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Unlike <see cref="TryReadFloat"/>, this method always consumes a token regardless of whether
    /// parsing succeeds.  Prefer <see cref="TryReadFloat"/> when the caller may need to fall back.
    /// </remarks>
    public bool ReadFloat(out float value)
    {
        if (!TryReadToken(out var token))
        {
            value = 0;
            return false;
        }

        return float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
