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
/// Describes the location and size of an encoded audio packet within a data buffer.
/// </summary>
/// <param name="Offset">The byte offset of the packet within the data buffer.</param>
/// <param name="Length">The length of the packet in bytes.</param>
public readonly record struct AudioPacket(int Offset, int Length);
