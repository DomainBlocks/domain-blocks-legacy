using DomainBlocks.V1.Abstractions.Subscriptions;
using DomainBlocks.V1.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Shopping.Domain.Events;
using Shopping.ReadModel.Db;
using Shopping.ReadModel.Model;

namespace Shopping.ReadModel.Projections;

public class ShoppingCartSummaryProjection :
    IEventStreamConsumer,
    IEventHandler<ShoppingSessionStarted>,
    IEventHandler<ItemAddedToShoppingCart>,
    IEventHandler<ItemRemovedFromShoppingCart>
{
    private const string CheckpointName = nameof(ShoppingCartSummaryProjection);
    private readonly IDbContextFactory<ShoppingCartDbContext> _dbContextFactory;
    private ShoppingCartDbContext? _currentDbContext;

    public ShoppingCartSummaryProjection(IDbContextFactory<ShoppingCartDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    private ShoppingCartDbContext DbContext => _currentDbContext ??= _dbContextFactory.CreateDbContext();

    public async Task OnEventAsync(EventHandlerContext<ShoppingSessionStarted> context)
    {
        var cart = await DbContext.ShoppingCartSummaries.FindAsync(
            [context.Event.SessionId], cancellationToken: context.CancellationToken);

        if (cart == null)
        {
            cart = new ShoppingCartSummary { SessionId = context.Event.SessionId };
            DbContext.ShoppingCartSummaries.Add(cart);
        }
    }

    public async Task OnEventAsync(EventHandlerContext<ItemAddedToShoppingCart> context)
    {
        var summary = await DbContext.ShoppingCartSummaries.FindAsync(
            [context.Event.SessionId], context.CancellationToken);

        if (summary != null)
        {
            summary.ItemCount++;
        }
    }

    public async Task OnEventAsync(EventHandlerContext<ItemRemovedFromShoppingCart> context)
    {
        var summary = await DbContext.ShoppingCartSummaries.FindAsync(
            [context.Event.SessionId], context.CancellationToken);

        if (summary != null)
        {
            summary.ItemCount--;
        }
    }

    public async Task OnInitializingAsync(CancellationToken cancellationToken)
    {
        var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
    }

    public async Task<SubscriptionPosition?> OnRestoreAsync(CancellationToken cancellationToken)
    {
        var bookmark = await DbContext.Checkpoints.FindAsync([CheckpointName], cancellationToken);
        return bookmark == null ? null : new SubscriptionPosition(bookmark.Position);
    }

    public async Task OnCheckpointAsync(SubscriptionPosition position, CancellationToken cancellationToken)
    {
        if (_currentDbContext == null)
        {
            // No activity since last checkpoint. Nothing to do.
            return;
        }

        var checkpoint = await DbContext.Checkpoints.FindAsync([CheckpointName], cancellationToken);
        if (checkpoint == null)
        {
            checkpoint = new Checkpoint { Name = CheckpointName };
            DbContext.Checkpoints.Add(checkpoint);
        }

        checkpoint.Position = position.Value;

        await DbContext.SaveChangesAsync(cancellationToken);
        await DisposeCurrentDbContextAsync();
    }

    private async ValueTask DisposeCurrentDbContextAsync()
    {
        if (_currentDbContext != null)
        {
            await _currentDbContext.DisposeAsync();
            _currentDbContext = null;
        }
    }
}