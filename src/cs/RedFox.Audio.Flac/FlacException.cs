// --------------------------------------------------------------------------------------
// RedFox Utility Library - MIT License
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

namespace RedFox.Audio.Flac;

/// <summary>
/// Represents an error that occurred during FLAC encoding or decoding operations.
/// </summary>
public sealed class FlacException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FlacException"/> class.
    /// </summary>
    public FlacException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FlacException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public FlacException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FlacException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public FlacException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
