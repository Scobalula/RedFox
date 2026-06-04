// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
namespace RedFox.GameExtraction.Hashing;

/// <summary>
/// Defines flags that can be associated with a name file to indicate certain properties or behaviors.
/// </summary>
[Flags]
public enum NameFileFlags : ulong
{
    /// <summary>
    /// Indicates no flags set.
    /// </summary>
    None,

    /// <summary>
    /// Indicates that the name file is compressed.
    /// </summary>
    Compressed = 1 << 0,

    /// <summary>
    /// Indicates that the name file contains metadata entries.
    /// </summary>
    Metadata = 1 << 1,
}
