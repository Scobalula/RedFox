namespace RedFox.Audio.Wwise;

public sealed class WwiseMediaNameResolver
{
    private readonly Dictionary<uint, WwiseNode> _nodes = [];

    private readonly Dictionary<uint, WwiseAction> _actions = [];

    private readonly Dictionary<string, List<WwiseEvent>> _eventsByBank = [];

    private readonly Dictionary<string, HashSet<uint>> _referencedMediaByBank = [];

    public const string TableName = "WwiseMediaTable";

    public void AddBank(string bankName, WwiseBank bank)
    {
        foreach (var node in bank.Nodes)
        {
            if (_nodes.TryGetValue(node.Id, out var existing))
                _nodes[node.Id] = node with { SourceIds = [.. existing.SourceIds.Union(node.SourceIds)] };
            else
                _nodes[node.Id] = node;
        }

        foreach (var action in bank.Actions)
            _actions[action.Id] = action;

        if (!_eventsByBank.TryGetValue(bankName, out var events))
            _eventsByBank[bankName] = events = [];

        events.AddRange(bank.Events);

        if (!_referencedMediaByBank.TryGetValue(bankName, out var media))
            _referencedMediaByBank[bankName] = media = [];

        media.UnionWith(bank.Nodes.SelectMany(x => x.SourceIds));
    }

    public Dictionary<uint, string> Resolve()
    {
        return Resolve(new Dictionary<uint, string>());
    }

    public Dictionary<uint, string> Resolve(IReadOnlyDictionary<uint, string> eventNames)
    {
        return Resolve(new Dictionary<uint, string>(), eventNames);
    }

    public Dictionary<uint, string> Resolve(IReadOnlyDictionary<uint, string> contextNames, IReadOnlyDictionary<uint, string> eventNames)
    {
        var childrenByParent = _nodes.Values.Where(x => x.ParentId != 0).ToLookup(x => x.ParentId);
        var playedMediaByBank = new Dictionary<string, HashSet<uint>>();

        foreach (var (bankName, events) in _eventsByBank)
        {
            var playedMedia = new HashSet<uint>();

            foreach (var playAction in events.SelectMany(x => x.ActionIds).Select(x => _actions.GetValueOrDefault(x)).Where(x => x is { IsPlay: true }))
                CollectMedia(playAction!.TargetId, childrenByParent, playedMedia, []);

            playedMediaByBank[bankName] = playedMedia;
        }

        var names = ResolveMostSpecificBank(_referencedMediaByBank);

        foreach (var (mediaId, name) in ResolveMostSpecificBank(playedMediaByBank))
            names[mediaId] = name;

        var eventsById = _eventsByBank.Values.SelectMany(events => events).ToLookup(item => item.Id);
        ApplyEventNames(names, contextNames, eventsById, childrenByParent);
        ApplyEventNames(names, eventNames, eventsById, childrenByParent);
        return names;
    }

    private void ApplyEventNames(Dictionary<uint, string> names, IReadOnlyDictionary<uint, string> eventNames, ILookup<uint, WwiseEvent> eventsById, ILookup<uint, WwiseNode> childrenByParent)
    {
        foreach (var (eventId, name) in eventNames.OrderBy(x => x.Value, StringComparer.Ordinal).Reverse())
        {
            HashSet<uint> media = [];

            foreach (var action in eventsById[eventId].SelectMany(x => x.ActionIds).Select(x => _actions.GetValueOrDefault(x)).Where(x => x is { IsPlay: true }))
                CollectMedia(action!.TargetId, childrenByParent, media, []);

            foreach (uint mediaId in media)
                names[mediaId] = name;
        }

    }

    private void CollectMedia(uint nodeId, ILookup<uint, WwiseNode> childrenByParent, HashSet<uint> media, HashSet<uint> visited)
    {
        if (!visited.Add(nodeId))
            return;

        if (_nodes.TryGetValue(nodeId, out var node))
            media.UnionWith(node.SourceIds);

        foreach (var child in childrenByParent[nodeId])
            CollectMedia(child.Id, childrenByParent, media, visited);
    }

    private static Dictionary<uint, string> ResolveMostSpecificBank(Dictionary<string, HashSet<uint>> mediaByBank)
    {
        var names = new Dictionary<uint, string>();
        var mediaCounts = new Dictionary<uint, int>();

        foreach (var (bankName, media) in mediaByBank.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            foreach (var mediaId in media)
            {
                if (names.ContainsKey(mediaId) && mediaCounts[mediaId] <= media.Count)
                    continue;

                names[mediaId] = bankName;
                mediaCounts[mediaId] = media.Count;
            }
        }

        return names;
    }
}
