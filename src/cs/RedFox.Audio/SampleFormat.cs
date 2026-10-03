// --------------------------------------------------------------------------------------
// RedFox Utility Library - MIT License
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

namespace RedFox.Audio;

/// <summary>
/// Specifies the data type and encoding used for individual audio samples.
/// </summary>
public enum SampleFormat
{
    /// <summary>
    /// Unsigned 8-bit integer samples.
    /// </summary>
    UInt8,

    /// <summary>
    /// Signed 16-bit integer samples.
    /// </summary>
    Int16,

    /// <summary>
    /// Signed 24-bit integer samples.
    /// </summary>
    Int24,

    /// <summary>
    /// Signed 32-bit integer samples.
    /// </summary>
    Int32,

    /// <summary>
    /// 32-bit floating point samples (single precision).
    /// </summary>
    Float32,

    /// <summary>
    /// 64-bit floating point samples (double precision).
    /// </summary>
    Float64,
}
