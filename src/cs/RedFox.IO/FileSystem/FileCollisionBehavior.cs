// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
namespace RedFox.IO.FileSystem;

/// <summary>
/// Specifies how a <see cref="VirtualFileSystem"/> handles adding a file to a path that is already occupied.
/// </summary>
public enum FileCollisionBehavior
{
    /// <summary>
    /// Throws an <see cref="IOException"/>.
    /// </summary>
    Throw,

    /// <summary>
    /// Keeps the existing file and does not add the new one.
    /// </summary>
    KeepExisting,

    /// <summary>
    /// Makes the new file visible and shadows the existing file, which becomes visible again once the new file is removed.
    /// </summary>
    Override,
}
