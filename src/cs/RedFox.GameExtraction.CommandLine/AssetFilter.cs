using System.IO.Enumeration;

namespace RedFox.GameExtraction.CommandLine;

internal sealed class AssetFilter(string? pattern, string? type)
{
    public const string TypePrefix = "type:";

    public string? Pattern { get; } = pattern;

    public string? Type { get; } = type;

    public bool IsEmpty => Pattern is null && Type is null;

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

    public static IEnumerable<string> GetCompletions(IEnumerable<Asset> assets)
    {
        return assets.Select(asset => asset.Type).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).Select(type => TypePrefix + type);
    }
}
