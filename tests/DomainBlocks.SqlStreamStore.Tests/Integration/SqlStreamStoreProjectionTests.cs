using DomainBlocks.Core.Projections.Builders;
using DomainBlocks.Core.Subscriptions;
using DomainBlocks.DependencyInjection;
using DomainBlocks.V1.DependencyInjection;
using DomainBlocks.V1.SqlStreamStore.Extensions;
using DomainBlocks.SqlStreamStore.Subscriptions;
using DomainBlocks.V1.Persistence;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Shopping.Domain;
using Shopping.Domain.Events;
using Shopping.WriteModel;
using SqlStreamStore;

namespace DomainBlocks.SqlStreamStore.Tests.Integration;

[TestFixture]
public class SqlStreamStoreProjectionTests
{
    [Test]
    [CancelAfter(10000)]
    public async Task WriteReadWithCheckpointTest()
    {
        var streamStore = new InMemoryStreamStore();

        // Setup write model
        var writeServices = new ServiceCollection();

        writeServices.AddEntityStore(config => config
            .UseSqlStreamStore(streamStore)
            .AddEntityAdapters(x => x.AddGenericFactoryFor(typeof(AggregateAdapter<>)))
            .MapEvents(x => x.MapAll<IDomainEvent>()));

        var writeServiceProvider = writeServices.BuildServiceProvider();

        var entityStore = writeServiceProvider.GetRequiredService<IEntityStore>();
        var shoppingCart = new ShoppingCart();
        shoppingCart.StartSession();
        shoppingCart.AddItem("Item 1");
        await entityStore.SaveAsync(shoppingCart);

        // Setup read model
        var readServices = new ServiceCollection();
        var itemsByCart = new Dictionary<Guid, List<string>>();
        long? lastPosition = null;
        var checkpointSignal = new TaskCompletionSource();
        var exception = new Exception("Event handler error");

        readServices.AddEventStreamSubscription(options =>
        {
            options
                .UseSqlStreamStore(o => o.WithInstance(streamStore))
                .FromAllEventsStream()
                .ProjectTo()
                .SingletonState(itemsByCart)
                .WithCheckpoints(x => x.PerEventCount(2))
                .OnCheckpoint((_, pos, _) =>
                {
                    lastPosition = pos;
                    checkpointSignal.SetResult();
                    return Task.CompletedTask;
                })
                .OnEventError((_, _, _) => Task.FromResult(EventErrorResolution.Retry))
                .When<ShoppingSessionStarted>((e, s) =>
                {
                    try
                    {
                        if (exception != null) throw exception;
                        s.Add(e.SessionId, []);
                    }
                    finally
                    {
                        exception = null;
                    }
                })
                .When<ItemAddedToShoppingCart>((e, s) => s[shoppingCart.Id].Add(e.Item));
        });

        var readServiceProvider = readServices.BuildServiceProvider();
        var subscription = readServiceProvider.GetRequiredService<IEventStreamSubscription>();

        var task = await Task.WhenAny(
            subscription.StartAsync(),
            subscription.WaitForCompletedAsync(),
            checkpointSignal.Task);

        Assert.That(task.Status, Is.Not.EqualTo(TaskStatus.Faulted), () => task.Exception!.Message);
        Assert.That(task.Status, Is.EqualTo(TaskStatus.RanToCompletion));
        Assert.That(itemsByCart, Contains.Key(shoppingCart.Id));
        Assert.That(itemsByCart[shoppingCart.Id], Is.EqualTo(new[] { "Item 1" }));
        Assert.That(lastPosition, Is.Not.Null);
        Assert.That(lastPosition, Is.EqualTo(1));
    }
}