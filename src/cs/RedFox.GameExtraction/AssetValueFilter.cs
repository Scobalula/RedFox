using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace RedFox.GameExtraction;

/// <summary>
/// Represents a key:value search condition such as bonecount:&gt;50, width:1024, or bonename:tag_.
/// </summary>
public sealed class AssetValueFilter
{
    private readonly bool _isNumeric;
    private readonly char _comparison;
    private readonly long _number;

    /// <summary>
    /// Gets the lower-case search key.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Gets the value text, including any leading &lt; or &gt; comparison.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AssetValueFilter"/> class.
    /// </summary>
    /// <param name="key">The search key, such as bonecount.</param>
    /// <param name="value">The number to compare, optionally prefixed with &lt; or &gt;, or the text to find in the key's names.</param>
    public AssetValueFilter(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrEmpty(value);

        Key		= key.ToLowerInvariant();
        Value	= value;

        bool hasComparison = value[0] is '<' or '>';

        _isNumeric	= long.TryParse(hasComparison ? value[1..] : value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _number);
        _comparison	= hasComparison ? value[0] : '=';
    }

    /// <summary>
    /// Parses a key:value search term.
    /// </summary>
    /// <param name="term">The search term to parse.</param>
    /// <param name="filter">The parsed filter when the term is a key:value condition.</param>
    /// <returns><see langword="true"/> when the term is a key:value condition; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(string term, [NotNullWhen(true)] out AssetValueFilter? filter)
    {
        int separator = term.IndexOf(':');

        if (separator <= 0 || separator == term.Length - 1 || !term[..separator].All(char.IsAsciiLetter) || term.StartsWith(AssetFilter.TypePrefix, StringComparison.OrdinalIgnoreCase))
        {
            filter = null;
            return false;
        }

        filter = new AssetValueFilter(term[..separator], term[(separator + 1)..]);
        return true;
    }

    /// <summary>
    /// Determines whether the asset exposes the key with a value satisfying this condition.
    /// </summary>
    /// <param name="asset">The asset to test.</param>
    /// <returns><see langword="true"/> when the asset satisfies the condition; otherwise, <see langword="false"/>.</returns>
    public bool Matches(Asset asset)
    {
        if (!_isNumeric)
        {
            return asset.TryGetSearchNames(Key, out IReadOnlyList<string> names) && names.Any(name => name.Contains(Value, StringComparison.OrdinalIgnoreCase));
        }

        if (!asset.TryGetSearchValue(Key, out long value))
        {
            return false;
        }

        return _comparison switch
        {
            '<'	=> value < _number,
            '>'	=> value > _number,
            _	=> value == _number,
        };
    }
}
