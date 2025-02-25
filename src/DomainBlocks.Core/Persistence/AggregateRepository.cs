namespace DomainBlocks.Core.Persistence;

public sealed class AggregateRepository(
    IEventsRepository eventsRepository,
    ISnapshotRepository snapshotRepository,
    Model model)
    : IAggregateRepository
{
    private readonly IEventsRepository _eventsRepository = eventsRepository ?? throw new ArgumentNullException(nameof(eventsRepository));
    private readonly ISnapshotRepository _snapshotRepository = snapshotRepository ?? throw new ArgumentNullException(nameof(snapshotRepository));
    private readonly Model _model = model ?? throw new ArgumentNullException(nameof(model));

    public async Task<LoadedAggregate<TAggregateState>> LoadAsync<TAggregateState>(
        string id,
        AggregateLoadStrategy loadStrategy = AggregateLoadStrategy.PreferSnapshot,
        CancellationToken cancellationToken = default)
    {
        if (id == null) throw new ArgumentNullException(nameof(id));

        var aggregateType = _model.GetAggregateType<TAggregateState>();
        var initialState = aggregateType.CreateNew();

        // If we choose to load solely from an event stream, then we need some initial state
        // onto which to apply the events.
        // TODO (DS): This logic seems wrong, as initial state is never null.
        if (initialState == null && loadStrategy == AggregateLoadStrategy.UseEventStream)
        {
            throw new ArgumentNullException(nameof(initialState));
        }

        var stateToAppendEventsTo = initialState;

        var streamName = aggregateType.MakeStreamKey(id);
        long loadStartPosition = 0;
        long? snapshotVersion = null;

        if (loadStrategy is AggregateLoadStrategy.UseSnapshot or AggregateLoadStrategy.PreferSnapshot)
        {
            var snapshotKey = aggregateType.MakeSnapshotKey(id);
            var (isSuccess, snapshot) =
                await _snapshotRepository.TryLoadSnapshotAsync<TAggregateState>(snapshotKey, cancellationToken);

            if (!isSuccess)
            {
                if (loadStrategy == AggregateLoadStrategy.UseSnapshot)
                {
                    throw new SnapshotDoesNotExistException(snapshotKey);
                }
            }
            else
            {
                snapshotVersion = snapshot!.Version;
                stateToAppendEventsTo = snapshot.SnapshotState;
                loadStartPosition = snapshot.Version + 1;
            }
        }

        if (stateToAppendEventsTo == null)
        {
            throw new InvalidOperationException("Snapshot was not found, and no initial state " +
                                                "was supplied onto which to append events");
        }

        var events = _eventsRepository.LoadEventsAsync(streamName, loadStartPosition, cancellationToken);
        var state = stateToAppendEventsTo;
        var eventCount = 0;

        await foreach (var @event in events.WithCancellation(cancellationToken))
        {
            state = aggregateType.InvokeEventApplier(state, @event);
            eventCount++;
        }

        var newVersion = loadStartPosition + eventCount - 1;

        return LoadedAggregate.Create(state, aggregateType, id, newVersion, snapshotVersion, eventCount);
    }

    public async Task<long> SaveAsync<TAggregateState>(
        LoadedAggregate<TAggregateState> loadedAggregate,
        Func<LoadedAggregate<TAggregateState>, bool>? snapshotPredicate = null,
        CancellationToken cancellationToken = default)
    {
        if (loadedAggregate == null) throw new ArgumentNullException(nameof(loadedAggregate));
        if (loadedAggregate.HasBeenSaved)
        {
            throw new ArgumentException("Aggregate has already been saved and must be loaded from persistence " +
                                        "before being saved again");
        }

        snapshotPredicate ??= _ => false;
        var aggregateType = _model.GetAggregateType<TAggregateState>();
        var streamName = aggregateType.MakeStreamKey(loadedAggregate.Id);

        var newVersion = await _eventsRepository.SaveEventsAsync(
            streamName, loadedAggregate.Version, loadedAggregate.EventsToPersist, cancellationToken);

        loadedAggregate.HasBeenSaved = true;

        if (snapshotPredicate(loadedAggregate))
        {
            var snapshotKey = aggregateType.MakeSnapshotKey(loadedAggregate.Id);

            await _snapshotRepository.SaveSnapshotAsync(
                snapshotKey, loadedAggregate.Version, loadedAggregate.State, cancellationToken: cancellationToken);
        }

        return newVersion;
    }

    public async Task SaveSnapshotAsync<TAggregateState>(
        VersionedAggregateState<TAggregateState> versionedState,
        CancellationToken cancellationToken = default)
    {
        if (versionedState == null) throw new ArgumentNullException(nameof(versionedState));

        var aggregateType = _model.GetAggregateType<TAggregateState>();
        var snapshotStreamId = aggregateType.MakeSnapshotKey(versionedState.AggregateState);

        await _snapshotRepository.SaveSnapshotAsync(
            snapshotStreamId,
            versionedState.Version,
            versionedState.AggregateState,
            cancellationToken: cancellationToken);
    }
}