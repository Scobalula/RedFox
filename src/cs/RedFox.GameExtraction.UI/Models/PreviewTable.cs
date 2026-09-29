using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace RedFox.GameExtraction.UI.Models;

/// <summary>
/// Flattens key/value payloads into a table. A sequence of pairs becomes a Key/Value table and a sequence of pair
/// sequences becomes one row per sequence with a column per distinct key.
/// </summary>
public sealed class PreviewTable
{
    private static readonly ConcurrentDictionary<Type, (PropertyInfo Key, PropertyInfo Value)?> PairProperties = new();

    private PreviewTable(IReadOnlyList<string> columns, IReadOnlyList<string[]> rows)
    {
        Columns = columns;
        Rows = rows;
    }

    /// <summary>
    /// Gets the column headers.
    /// </summary>
    public IReadOnlyList<string> Columns { get; }

    /// <summary>
    /// Gets the formatted cell values of each row, indexed by column.
    /// </summary>
    public IReadOnlyList<string[]> Rows { get; }

    /// <summary>
    /// Attempts to build a table from a key/value payload.
    /// </summary>
    /// <param name="payload">The payload returned by an asset handler.</param>
    /// <param name="table">The resulting table when the payload is tabular.</param>
    /// <returns><see langword="true"/> when the payload was a sequence of pairs or a sequence of pair sequences.</returns>
    public static bool TryCreate(object? payload, [NotNullWhen(true)] out PreviewTable? table)
    {
        table = null;
        if (payload is null or string or byte[])
        {
            return false;
        }
        if (payload is not IEnumerable enumerable)
        {
            return false;
        }

        List<object?> items = [.. enumerable.Cast<object?>()];
        if (items.Count == 0)
        {
            table = payload is IDictionary ? new PreviewTable(["Key", "Value"], []) : null;
            return table is not null;
        }

        if (items.TrueForAll(IsPair))
        {
            table = new PreviewTable(["Key", "Value"], [.. items.Select(item => ReadPair(item!)).Select(pair => new[] { FormatValue(pair.Key), FormatValue(pair.Value) })]);
            return true;
        }

        List<List<(object? Key, object? Value)>> rows = [];
        foreach (object? item in items)
        {
            if (item is null or string || item is not IEnumerable row)
            {
                return false;
            }

            List<object?> cells = [.. row.Cast<object?>()];
            if (!cells.TrueForAll(IsPair))
            {
                return false;
            }

            rows.Add([.. cells.Select(cell => ReadPair(cell!))]);
        }

        List<string> columns = [];
        Dictionary<string, int> columnIndices = new(StringComparer.Ordinal);
        foreach ((object? key, _) in rows.SelectMany(row => row))
        {
            string column = FormatValue(key);
            if (columnIndices.TryAdd(column, columns.Count))
            {
                columns.Add(column);
            }
        }

        List<string[]> cellRows = new(rows.Count);
        foreach (List<(object? Key, object? Value)> row in rows)
        {
            string[] cells = new string[columns.Count];
            Array.Fill(cells, string.Empty);
            foreach ((object? key, object? value) in row)
            {
                cells[columnIndices[FormatValue(key)]] = FormatValue(value);
            }

            cellRows.Add(cells);
        }

        table = new PreviewTable(columns, cellRows);
        return true;
    }

    private static bool IsPair(object? item)
    {
        return item is DictionaryEntry or ITuple { Length: 2 } || (item is not null && GetPairProperties(item.GetType()) is not null);
    }

    private static (object? Key, object? Value) ReadPair(object item)
    {
        return item switch
        {
            DictionaryEntry entry => (entry.Key, entry.Value),
            ITuple tuple => (tuple[0], tuple[1]),
            _ => GetPairProperties(item.GetType()) is { } properties ? (properties.Key.GetValue(item), properties.Value.GetValue(item)) : throw new InvalidOperationException($"'{item.GetType().Name}' is not a key/value pair."),
        };
    }

    private static (PropertyInfo Key, PropertyInfo Value)? GetPairProperties(Type type)
    {
        return PairProperties.GetOrAdd(type, static pairType =>
        {
            if (!pairType.IsGenericType || pairType.GetGenericTypeDefinition() != typeof(KeyValuePair<,>))
            {
                return null;
            }

            return (pairType.GetProperty(nameof(KeyValuePair<,>.Key))!, pairType.GetProperty(nameof(KeyValuePair<,>.Value))!);
        });
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => string.Empty,
            string text => text,
            byte[] bytes => $"{bytes.Length:N0} bytes",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            IEnumerable sequence => string.Join(", ", sequence.Cast<object?>().Take(17).Select((item, index) => index == 16 ? "…" : FormatValue(item))),
            _ => value.ToString() ?? string.Empty,
        };
    }
}
