// --------------------------------------------------------------------------------------
// RedFox Utility Library - MIT License
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

namespace RedFox.Compression
{
    public class CompressionException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CompressionException"/> class.
        /// </summary>
        public CompressionException() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="CompressionException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public CompressionException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="CompressionException"/> class with a specified error message and a reference to the inner exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public CompressionException(string message, Exception innerException) : base(message, innerException) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="CompressionException"/> class with a specified error message and additional details.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="operation">The operation that failed (e.g., "compression" or "decompression").</param>
        /// <param name="details">Additional details about the error.</param>
        public CompressionException(string message, string operation, string details)
            : base($"{message} (Operation: {operation}, Details: {details})") { }
    }
}
