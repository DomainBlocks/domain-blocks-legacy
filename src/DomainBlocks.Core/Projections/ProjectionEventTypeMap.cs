namespace DomainBlocks.Core.Projections;

public sealed class ProjectionEventTypeMap
{
    private readonly Dictionary<string, HashSet<Type>> _eventNameToTypeMap = new();

    public ProjectionEventTypeMap()
    {
    }

    private ProjectionEventTypeMap(ProjectionEventTypeMap copyFrom)
    {
        _eventNameToTypeMap = copyFrom._eventNameToTypeMap.ToDictionary(x => x.Key, x => new HashSet<Type>(x.Value));
    }

    public ProjectionEventTypeMap Add(Type eventType, params string[] eventNames)
    {
        return Add(eventType, eventNames.AsEnumerable());
    }

    public ProjectionEventTypeMap Add(Type eventType, IEnumerable<string> eventNames)
    {
        var copy = new ProjectionEventTypeMap(this);

        foreach (var eventName in eventNames)
        {
            if (copy._eventNameToTypeMap.TryGetValue(eventName, out var eventTypes))
            {
                eventTypes.Add(eventType);
            }
            else
            {
                copy._eventNameToTypeMap.Add(eventName, [eventType]);
            }
        }

        return copy;
    }

    public ProjectionEventTypeMap Merge(ProjectionEventTypeMap eventTypeMap)
    {
        var merged = this;

        foreach (var (eventName, eventTypes) in eventTypeMap._eventNameToTypeMap)
        {
            merged = eventTypes.Aggregate(merged, (acc, next) => acc.Add(next, eventName));
        }

        return merged;
    }

    public IEnumerable<Type> GetClrTypes(string eventName)
    {
        if (eventName == null) throw new ArgumentNullException(nameof(eventName));

        if (!_eventNameToTypeMap.TryGetValue(eventName, out var types))
        {
            yield break;
        }

        foreach (var type in types)
        {
            yield return type;
        }
    }

    public bool IsMapped(Type eventType) => _eventNameToTypeMap.Values.Any(x => x.Contains(eventType));
}