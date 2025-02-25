namespace DomainBlocks.V1.Tests.Integration.Model;

public abstract class EntityBase<TState> where TState : StateBase<TState>, new()
{
    private readonly List<object> _raisedEvents = [];
    private TState _state = new();

    public abstract string Id { get; }

    public TState State
    {
        get => _state;
        init => _state = value;
    }

    public IEnumerable<object> RaisedEvents => _raisedEvents.AsReadOnly();

    protected void Raise<TEvent>(TEvent @event)
    {
        if (@event == null) throw new ArgumentNullException(nameof(@event));
        _state = _state.Apply(@event);
        _raisedEvents.Add(@event);
    }
}