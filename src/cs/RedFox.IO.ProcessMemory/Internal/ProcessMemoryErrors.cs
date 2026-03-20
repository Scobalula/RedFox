// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace RedFox.IO.ProcessMemory.Internal
{
    internal static class ProcessMemoryErrors
    {
        public static PlatformNotSupportedException CreateUnsupportedPlatformException(string operation)
        {
            string message = $"Process memory {operation} is not supported on {RuntimeInformation.OSDescription}.";
            return new PlatformNotSupportedException(message);
        }

        public static InvalidOperationException CreateOpenProcessException(int processId, int errorCode)
        {
            string message = $"Failed to open process {processId}. Native error: {FormatError(errorCode)}.";
            return new InvalidOperationException(message);
        }

        public static InvalidOperationException CreateNativeFailure(string operation, int processId, nint address, int requestedLength, int errorCode)
        {
            string operationText = $"{operation} failed for process {processId} at 0x{address:X}";
            string lengthText = $"with length {requestedLength}.";
            string nativeErrorText = $"Native error: {FormatError(errorCode)}.";
            string message = $"{operationText} {lengthText} {nativeErrorText}";
            return new InvalidOperationException(message);
        }

        public static IOException CreatePartialTransfer(string operation, int processId, nint address, int requestedLength, nint transferredLength)
        {
            string partialText = $"Partial {operation} for process {processId} at 0x{address:X}.";
            string requestedText = $"Requested {requestedLength} bytes.";
            string transferredText = $"Transferred {transferredLength} bytes.";
            string message = $"{partialText} {requestedText} {transferredText}";
            return new IOException(message);
        }

        private static string FormatError(int errorCode)
        {
            return $"{errorCode} ({new Win32Exception(errorCode).Message})";
        }
    }
}
