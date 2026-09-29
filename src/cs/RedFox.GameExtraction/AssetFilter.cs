using System.IO.Enumeration;

namespace RedFox.GameExtraction;

/// <summary>
/// Represents a filter for searching and matching assets by name pattern and type.
/// </summary>
public sealed class AssetFilter(string? pattern, string? type)
{
    /// <summary>
    /// The prefix used to denote a type filter in asset filter arguments.
    /// </summary>
    public const string TypePrefix = "type:";

    /// <summary>
    /// Gets the name pattern used to filter assets. This may contain wildcard characters (* and ?).
    /// </summary>
    public string? Pattern { get; } = pattern;

    /// <summary>
    /// Gets the asset type used to filter assets. If specified, only assets matching this type will pass the filter.
    /// </summary>
    public string? Type { get; } = type;

    /// <summary>
    /// Gets a value indicating whether this filter has no constraints (both pattern and type are null).
    /// </summary>
    public bool IsEmpty => Pattern is null && Type is null;

    /// <summary>
    /// Parses command-line arguments into an asset filter.
    /// Arguments prefixed with "type:" are treated as type filters; all other arguments are treated as name patterns and joined with spaces.
    /// </summary>
    /// <param name="arguments">The arguments to parse.</param>
    /// <returns>A new AssetFilter instance with the parsed pattern and type.</returns>
    public static AssetFilter Parse(IReadOnlyList<string> arguments)
    {
        List<string> patterns = [];
        string? type = null;

        foreach (string argument in arguments)
        {
            if (argument.StartsWith(TypePrefix, StringComparison.OrdinalIgnoreCase))
            {
                type = argument[TypePrefix.Length..];
            }
            else
            {
                patterns.Add(argument);
            }
        }

        return new AssetFilter(patterns.Count == 0 ? null : string.Join(' ', patterns), string.IsNullOrEmpty(type) ? null : type);
    }

    /// <summary>
    /// Determines whether the specified asset matches this filter's constraints.
    /// Returns true if the asset's type matches (if a type constraint exists) and the asset's name matches the pattern (if a pattern constraint exists).
    /// </summary>
    /// <param name="asset">The asset to test.</param>
    /// <returns>true if the asset matches the filter; otherwise, false.</returns>
    public bool Matches(Asset asset)
    {
        if (Type is not null && !asset.Type.Equals(Type, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (Pattern is null)
        {
            return true;
        }

        if (Pattern.AsSpan().IndexOfAny('*', '?') >= 0)
        {
            return FileSystemName.MatchesSimpleExpression(Pattern, asset.Name, ignoreCase: true);
        }

        return asset.Name.Contains(Pattern, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets completion suggestions for asset type filters by extracting all unique types from the specified assets.
    /// Each completion is prefixed with the type prefix constant.
    /// </summary>
    /// <param name="assets">The assets from which to extract types.</param>
    /// <returns>An enumerable of completion strings, each in the format "type:&lt;typename&gt;", sorted alphabetically.</returns>
    public static IEnumerable<string> GetCompletions(IEnumerable<Asset> assets)
    {
        return assets.Select(asset => asset.Type).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).Select(type => TypePrefix + type);
    }
}
