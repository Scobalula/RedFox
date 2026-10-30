using System.IO.Enumeration;

namespace RedFox.GameExtraction;

/// <summary>
/// Represents a filter for searching and matching assets by name pattern, type, exclusions, and key:value conditions.
/// </summary>
public sealed class AssetFilter
{
    /// <summary>
    /// The prefix used to denote a type filter in asset filter arguments.
    /// </summary>
    public const string TypePrefix = "type:";

    /// <summary>
    /// Gets the name pattern used to filter assets. This may contain wildcard characters (* and ?).
    /// </summary>
    public string? Pattern { get; }

    /// <summary>
    /// Gets the asset type used to filter assets. If specified, only assets matching this type will pass the filter.
    /// </summary>
    public string? Type { get; }

    /// <summary>
    /// Gets the name patterns that assets must not match.
    /// </summary>
    public IReadOnlyList<string> Exclusions { get; }

    /// <summary>
    /// Gets the key:value conditions that assets must all satisfy.
    /// </summary>
    public IReadOnlyList<AssetValueFilter> Values { get; }

    /// <summary>
    /// Gets a value indicating whether this filter has no constraints.
    /// </summary>
    public bool IsEmpty => Pattern is null && Type is null && Exclusions.Count == 0 && Values.Count == 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="AssetFilter"/> class.
    /// </summary>
    /// <param name="pattern">The name pattern, or <see langword="null"/> to match any name.</param>
    /// <param name="type">The asset type, or <see langword="null"/> to match any type.</param>
    public AssetFilter(string? pattern, string? type) : this(pattern, type, [], [])
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AssetFilter"/> class with exclusions and key:value conditions.
    /// </summary>
    /// <param name="pattern">The name pattern, or <see langword="null"/> to match any name.</param>
    /// <param name="type">The asset type, or <see langword="null"/> to match any type.</param>
    /// <param name="exclusions">The name patterns that assets must not match.</param>
    /// <param name="values">The key:value conditions that assets must all satisfy.</param>
    public AssetFilter(string? pattern, string? type, IReadOnlyList<string> exclusions, IReadOnlyList<AssetValueFilter> values)
    {
        Pattern		= pattern;
        Type		= type;
        Exclusions	= exclusions;
        Values		= values;
    }

    /// <summary>
    /// Parses command-line arguments into an asset filter.
    /// Arguments prefixed with "type:" are type filters, "!" prefixes exclusions, key:value arguments are value conditions, and all other arguments are name patterns joined with spaces.
    /// </summary>
    /// <param name="arguments">The arguments to parse.</param>
    /// <returns>A new AssetFilter instance with the parsed constraints.</returns>
    public static AssetFilter Parse(IReadOnlyList<string> arguments)
    {
        List<string> patterns			= [];
        List<string> exclusions			= [];
        List<AssetValueFilter> values	= [];

        string? type = null;

        foreach (string argument in arguments)
        {
            if (argument.StartsWith(TypePrefix, StringComparison.OrdinalIgnoreCase))
            {
                type = argument[TypePrefix.Length..];
            }
            else if (argument.Length > 1 && argument[0] == '!')
            {
                exclusions.Add(argument[1..]);
            }
            else if (AssetValueFilter.TryParse(argument, out AssetValueFilter? value))
            {
                values.Add(value);
            }
            else
            {
                patterns.Add(argument);
            }
        }

        string? pattern		= patterns.Count == 0 ? null : string.Join(' ', patterns);
        string? typeFilter	= string.IsNullOrEmpty(type) ? null : type;

        return new AssetFilter(pattern, typeFilter, exclusions, values);
    }

    /// <summary>
    /// Determines whether the specified asset matches this filter's constraints.
    /// </summary>
    /// <param name="asset">The asset to test.</param>
    /// <returns>true if the asset matches the filter; otherwise, false.</returns>
    public bool Matches(Asset asset)
    {
        if (Type is not null && !asset.Type.Equals(Type, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (Pattern is not null && !MatchesName(Pattern, asset.Name))
        {
            return false;
        }

        return !Exclusions.Any(exclusion => MatchesName(exclusion, asset.Name)) && Values.All(value => value.Matches(asset));
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

    private static bool MatchesName(string pattern, string name)
    {
        if (pattern.AsSpan().IndexOfAny('*', '?') >= 0)
        {
            return FileSystemName.MatchesSimpleExpression(pattern, name, ignoreCase: true);
        }

        return name.Contains(pattern, StringComparison.OrdinalIgnoreCase);
    }
}
