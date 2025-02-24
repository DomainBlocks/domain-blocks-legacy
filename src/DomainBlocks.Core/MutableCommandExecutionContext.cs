namespace DomainBlocks.Core;

public sealed class MutableCommandExecutionContext<TAggregate, TEventBase> : ICommandExecutionContext<TAggregate>
{
    private readonly IMutableAggregateType<TAggregate> _aggregateType;
    private readonly List<object> _raisedEvents = [];

    public MutableCommandExecutionContext(TAggregate state, IMutableAggregateType<TAggregate> aggregateType)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
        _aggregateType = aggregateType ?? throw new ArgumentNullException(nameof(aggregateType));
    }

    public TAggregate State { get; }
    public IReadOnlyCollection<object> RaisedEvents => _raisedEvents.AsReadOnly();

    public TCommandResult ExecuteCommand<TCommandResult>(Func<TAggregate, TCommandResult> commandExecutor)
    {
        if (commandExecutor == null) throw new ArgumentNullException(nameof(commandExecutor));

        if (_aggregateType.CanSelectRaisedEventsFromAggregate)
        {
            var commandResult = commandExecutor(State);
            var raisedEvents = _aggregateType.SelectRaisedEvents(State);
            _raisedEvents.Clear();
            _raisedEvents.AddRange(raisedEvents);

            return commandResult;
        }

        {
            // We attempt to get the command result type before executing the command. This ensures that we throw
            // without executing the command if the type is missing.
            var commandResultType = _aggregateType.GetCommandResultType<TCommandResult>();
            var commandResult = commandExecutor(State);

            var raisedEvents = commandResultType.SelectEventsAndUpdateStateIfRequired(
                commandResult, State, _aggregateType.InvokeEventApplier);

            _raisedEvents.AddRange(raisedEvents);

            // If the command result is an event enumerable, return the materialized events to avoid multiple
            // enumeration.
            return typeof(TCommandResult) == typeof(IEnumerable<TEventBase>)
                ? (TCommandResult)raisedEvents.Cast<TEventBase>()
                : commandResult;
        }
    }

    public void ExecuteCommand(Action<TAggregate> commandExecutor)
    {
        if (commandExecutor == null) throw new ArgumentNullException(nameof(commandExecutor));

        if (!_aggregateType.CanSelectRaisedEventsFromAggregate)
        {
            throw new InvalidOperationException(
                "Cannot execute void command. Aggregate has no raised events selector specified.");
        }

        commandExecutor(State);
        var raisedEvents = _aggregateType.SelectRaisedEvents(State);
        _raisedEvents.Clear();
        _raisedEvents.AddRange(raisedEvents);
    }
}