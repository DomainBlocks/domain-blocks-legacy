using Shopping.Domain.Events;

namespace DomainBlocks.V1.Tests.Integration.Model;

public class MutableShoppingCart : MutableEntityBase
{
    private Guid _sessionId = Guid.Empty;
    private readonly List<ShoppingCartItem> _items = [];

    public override Guid Id => _sessionId;

    public IReadOnlyList<ShoppingCartItem> Items => _items;

    public void AddItem(ShoppingCartItem item)
    {
        if (Id == Guid.Empty)
        {
            Raise(new ShoppingSessionStarted(item.SessionId));
        }

        Raise(new ItemAddedToShoppingCart(item.SessionId, item.Name));
    }

    public void Apply(ShoppingSessionStarted @event)
    {
        _sessionId = @event.SessionId;
    }

    public void Apply(ItemAddedToShoppingCart @event)
    {
        _items.Add(new ShoppingCartItem(@event.SessionId, @event.Item));
    }
}