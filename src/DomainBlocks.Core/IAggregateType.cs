namespace DomainBlocks.Core;

public interface IAggregateType : IEventSourcedEntityType;

public interface IAggregateType<TAggregate> : IAggregateType, IEventSourcedEntityType<TAggregate>
{
    ICommandExecutionContext<TAggregate> CreateCommandExecutionContext(TAggregate aggregate);
    TAggregate InvokeEventApplier(TAggregate aggregate, object @event);
}