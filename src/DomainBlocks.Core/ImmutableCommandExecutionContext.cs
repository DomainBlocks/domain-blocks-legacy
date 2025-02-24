namespace DomainBlocks.Core;

public sealed class ImmutableCommandExecutionContext<TAggregate, TEventBase> : ICommandExecutionContext<TAggregate>
{
    private readonly IImmutableAggregateType<TAggregate> _aggregateType;
    private readonly List<object> _raisedEvents = [];
    private TAggregate _state;

    public ImmutableCommandExecutionContext(
        TAggregate state, IImmutableAggregateType<TAggregate> aggregateType)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _aggregateType = aggregateType ?? throw new ArgumentNullException(nameof(aggregateType));
    }

    public TAggregate State => _state;
    public IReadOnlyCollection<object> RaisedEvents => _raisedEvents;

    public TCommandResult ExecuteCommand<TCommandResult>(Func<TAggregate, TCommandResult> commandExecutor)
    {
        if (commandExecutor == null) throw new ArgumentNullException(nameof(commandExecutor));

        // We attempt to get the command result type before executing the command. This ensures that we throw without
        // executing the command if the type is missing.
        var commandResultType = _aggregateType.GetCommandResultType<TCommandResult>();
        var commandResult = commandExecutor(State);

        var raisedEvents = commandResultType.SelectEventsAndUpdateStateIfRequired(
            commandResult, ref _state, _aggregateType.InvokeEventApplier);

        _raisedEvents.AddRange(raisedEvents);

        // If the command result is an event enumerable, return the materialized events to avoid multiple enumeration.
        return typeof(TCommandResult) == typeof(IEnumerable<TEventBase>)
            ? (TCommandResult)raisedEvents.Cast<TEventBase>()
            : commandResult;
    }

    public void ExecuteCommand(Action<TAggregate> commandExecutor)
    {
        throw new InvalidOperationException("Cannot invoke a void command on an immutable aggregate type.");
    }
}