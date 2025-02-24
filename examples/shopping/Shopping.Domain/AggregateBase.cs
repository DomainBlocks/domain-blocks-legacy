using Shopping.Domain.Events;

namespace Shopping.Domain;

public abstract class AggregateBase
{
    private readonly List<IDomainEvent> _raisedEvents = [];

    public abstract Guid Id { get; }
    public IEnumerable<IDomainEvent> RaisedEvents => _raisedEvents.AsReadOnly();

    public void Apply(IDomainEvent @event)
    {
        ((dynamic)this).Apply((dynamic)@event);
    }

    protected void Raise<TEvent>(TEvent @event) where TEvent : IDomainEvent
    {
        if (@event == null) throw new ArgumentNullException(nameof(@event));
        Apply(@event);
        _raisedEvents.Add(@event);
    }
}