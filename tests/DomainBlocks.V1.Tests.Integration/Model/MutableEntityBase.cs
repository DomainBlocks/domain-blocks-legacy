namespace DomainBlocks.V1.Tests.Integration.Model;

public abstract class MutableEntityBase
{
    private readonly List<object> _raisedEvents = [];

    public abstract Guid Id { get; }
    public IEnumerable<object> RaisedEvents => _raisedEvents.AsReadOnly();

    public void Apply(object @event)
    {
        ((dynamic)this).Apply((dynamic)@event);
    }

    protected void Raise<TEvent>(TEvent @event)
    {
        if (@event == null) throw new ArgumentNullException(nameof(@event));
        Apply(@event);
        _raisedEvents.Add(@event);
    }
}