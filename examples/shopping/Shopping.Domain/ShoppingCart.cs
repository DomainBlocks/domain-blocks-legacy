using Shopping.Domain.Events;

namespace Shopping.Domain;

public class ShoppingCart : AggregateBase
{
    private Guid _sessionId;
    private readonly HashSet<string> _items = [];

    public override Guid Id => _sessionId;

    public void StartSession()
    {
        EnsureSessionIsNew();
        Raise(new ShoppingSessionStarted(Guid.NewGuid()));
    }

    public void AddItem(string item)
    {
        EnsureSessionIsStarted();

        if (!_items.Contains(item))
            Raise(new ItemAddedToShoppingCart(Id, item));
    }

    public void RemoveItem(string item)
    {
        EnsureSessionIsStarted();

        if (_items.Contains(item))
            Raise(new ItemRemovedFromShoppingCart(Id, item));
    }

    public void Apply(ShoppingSessionStarted @event)
    {
        _sessionId = @event.SessionId;
    }

    public void Apply(ItemAddedToShoppingCart @event)
    {
        _items.Add(@event.Item);
    }

    public void Apply(ItemRemovedFromShoppingCart @event)
    {
        _items.Remove(@event.Item);
    }

    private void EnsureSessionIsNew()
    {
        if (Id != Guid.Empty)
            throw new InvalidOperationException("Shopping session has already started.");
    }

    private void EnsureSessionIsStarted()
    {
        if (Id == Guid.Empty)
            throw new InvalidOperationException("Shopping session not started.");
    }
}