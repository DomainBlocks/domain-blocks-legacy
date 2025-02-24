using System.Text.Json;
using DomainBlocks.V1.EventStoreDb.Extensions;
using DomainBlocks.V1.SqlStreamStore;
using DomainBlocks.V1.SqlStreamStore.Extensions;
using DomainBlocks.ThirdParty.SqlStreamStore.Postgres;
using DomainBlocks.V1.Abstractions.Exceptions;
using DomainBlocks.V1.Persistence;
using DomainBlocks.V1.Persistence.Builders;
using DomainBlocks.V1.Persistence.Entities;
using DomainBlocks.V1.Persistence.Extensions;
using DomainBlocks.V1.Tests.Integration.Adapters;
using DomainBlocks.V1.Tests.Integration.Model;
using Shopping.Domain.Events;
using JsonSerializer = DomainBlocks.V1.Serialization.JsonSerializer;

namespace DomainBlocks.V1.Tests.Integration;

[TestFixture]
public class EntityStoreTests
{
    private static readonly IEntityStore SqlStreamStoreEntityStore;
    private static readonly IEntityStore EventStoreDbEntityStore;

    static EntityStoreTests()
    {
        // Still using the pre-Npgsql 6.0 timestamp behaviour.
        // See: https://www.npgsql.org/doc/types/datetime.html#timestamps-and-timezones
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        var streamStoreSettings = new PostgresStreamStoreSettings(ConnectionStrings.PostgresStreamStore);
        var streamStore = new PostgresStreamStore(streamStoreSettings);
        streamStore.CreateSchemaIfNotExists().Wait();

        var jsonOptions = new JsonSerializerOptions();

        var eventMapper = new EventMapperBuilder()
            .MapAll<IDomainEvent>(x => x.FromAssemblyOf<ShoppingSessionStarted>())
            .UseJsonSerialization(jsonOptions)
            .Build();

        var storeConfigBuilder = new EntityStoreConfigBuilder()
            .AddEntityAdapters(adapters => adapters
                .AddGenericFactoryFor(typeof(EntityAdapter<,>))
                .WithConstructorArgs(123, "ABC"))
            .SetEventMapper(eventMapper);

        var sqlStreamStoreConfig = storeConfigBuilder.UseSqlStreamStore(streamStore).Build();
        var eventStoreDbConfig = storeConfigBuilder.UseEventStoreDb(ConnectionStrings.EventStoreDb).Build();

        SqlStreamStoreEntityStore = new EntityStore(sqlStreamStoreConfig);
        EventStoreDbEntityStore = new EntityStore(eventStoreDbConfig);
    }

    [Test]
    [TestCaseSource(nameof(TestCases))]
    public async Task WriteToExpectedNewStream_WhenNoStream_Succeeds(IEntityStore store)
    {
        var entity = new ShoppingCart();
        entity.AddItem(new ShoppingCartItem(Guid.NewGuid(), "Foo"));
        entity.AddItem(new ShoppingCartItem(Guid.NewGuid(), "Bar"));
        await store.SaveAsync(entity);

        var reloadedEntity = await store.LoadAsync<ShoppingCart>(entity.State.SessionId.ToString());

        Assert.That(reloadedEntity.State.SessionId, Is.EqualTo(entity.State.SessionId));
        Assert.That(reloadedEntity.State.Items, Is.EqualTo(entity.State.Items));
    }

    [Test]
    [TestCaseSource(nameof(TestCases))]
    public async Task WriteToExpectedNewStream_WhenStreamExists_ThrowsWrongVersionException(IEntityStore store)
    {
        var entity1 = new ShoppingCart();
        entity1.AddItem(new ShoppingCartItem(Guid.NewGuid(), "Foo"));
        await store.SaveAsync(entity1);

        // Attempting to write a new state stream for the same ID should fail.
        var entity2 = new ShoppingCart
        {
            State = new ShoppingCartState { SessionId = entity1.State.SessionId }
        };

        entity2.AddItem(new ShoppingCartItem(entity1.State.SessionId, "Bar"));

        await Assert.ThatAsync(() => store.SaveAsync(entity2), Throws.TypeOf<WrongExpectedVersionException>());
    }

    [Test]
    [TestCaseSource(nameof(TestCases))]
    public async Task WriteToExpectedExisingStream_WhenNoStream_Succeeds(IEntityStore store)
    {
        var id = Guid.NewGuid();

        var entity = await store.LoadOrCreateAsync<ShoppingCart>(id.ToString());
        entity.AddItem(new ShoppingCartItem(id, "Foo"));
        await store.SaveAsync(entity);

        var reloadedEntity = await store.LoadAsync<ShoppingCart>(id.ToString());

        Assert.That(reloadedEntity.State.SessionId, Is.EqualTo(entity.State.SessionId));
        Assert.That(reloadedEntity.State.Items, Is.EqualTo(entity.State.Items));
    }

    [Test]
    [TestCaseSource(nameof(TestCases))]
    public async Task WriteToExpectedExisingStream_WhenStreamExists_Succeeds(IEntityStore store)
    {
        var entity1A = new ShoppingCart();
        entity1A.AddItem(new ShoppingCartItem(Guid.NewGuid(), "Foo"));
        await store.SaveAsync(entity1A);

        var entity1B = await store.LoadAsync<ShoppingCart>(entity1A.State.SessionId.ToString());
        entity1B.AddItem(new ShoppingCartItem(Guid.NewGuid(), "Bar"));
        await store.SaveAsync(entity1B);

        var reloadedEntity = await store.LoadAsync<ShoppingCart>(entity1A.State.SessionId.ToString());

        Assert.That(reloadedEntity.State.SessionId, Is.EqualTo(entity1A.State.SessionId));
        Assert.That(reloadedEntity.State.Items, Is.EqualTo(entity1B.State.Items));
    }
    
    [Test]
    [TestCaseSource(nameof(TestCases))]
    public void LoadAsync_WhenStreamDoesNotExist_ThrowsStreamNotFoundException(IEntityStore store)
    {
        const string id = "cart-1";
        
        var exception = Assert.ThrowsAsync<StreamNotFoundException>(() => store.LoadAsync<ShoppingCart>(id));
        Assert.That(exception?.Message, Is.EqualTo("Stream 'shoppingCart-cart-1' could not be found."));
    }
    
    [Test]
    [TestCaseSource(nameof(TestCases))]
    public async Task LoadAsync_WhenStreamExists_Succeeds(IEntityStore store)
    {
        var shoppingCart = new ShoppingCart();
        shoppingCart.AddItem(new ShoppingCartItem(Guid.NewGuid(), "Item 1"));
        await store.SaveAsync(shoppingCart);

        var entity = store.LoadAsync<ShoppingCart>(shoppingCart.Id);
        Assert.That(entity, Is.Not.Null);
    }
    
    [Test]
    [TestCaseSource(nameof(TestCases))]
    public async Task CreateOrLoadAsync_WhenStreamDoesNotExist_Succeeds(IEntityStore store)
    {
        var entity = await store.LoadOrCreateAsync<ShoppingCart>("cart-1");
        
        Assert.That(entity, Is.Not.Null);
    }

    [Test]
    public async Task MutableScenario()
    {
        var streamStoreSettings = new PostgresStreamStoreSettings(ConnectionStrings.PostgresStreamStore);
        var streamStore = new PostgresStreamStore(streamStoreSettings);

        var config = new EntityStoreConfigBuilder()
            .UseSqlStreamStore(streamStore)
            .AddEntityAdapters(x => x.Add(new MutableShoppingCartEntityAdapter()))
            .MapEvents(x => x.MapAll<IDomainEvent>())
            .Build();

        var store = new EntityStore(config);

        var entity = new MutableShoppingCart();
        var sessionId = Guid.NewGuid();
        entity.AddItem(new ShoppingCartItem(sessionId, "Foo"));
        entity.AddItem(new ShoppingCartItem(sessionId, "Bar"));
        await store.SaveAsync(entity);

        var reloadedEntity = await store.LoadAsync<MutableShoppingCart>(entity.Id.ToString());

        Assert.That(reloadedEntity.Id, Is.EqualTo(entity.Id));
        Assert.That(reloadedEntity.Items, Is.EqualTo(entity.Items));
    }

    [Test]
    public async Task FunctionalEntityWrapperScenario()
    {
        var config = new EntityStoreConfigBuilder()
            .UseEventStoreDb(ConnectionStrings.EventStoreDb)
            .AddEntityAdapters(x => x.AddGenericFactoryFor(typeof(FunctionalEntityWrapperAdapter<>)))
            .MapEvents(x => x.MapAll<IDomainEvent>())
            .Configure<FunctionalEntityWrapper<FunctionalShoppingCart>>(config =>
            {
                // Avoid prefix of "functionalEntityWrapper`1". Consider how we deal with this going forward.
                config.SetStreamNamePrefix("functionalShoppingCart");
            })
            .Build();

        var store = new EntityStore(config);

        var entity = new FunctionalEntityWrapper<FunctionalShoppingCart>();
        entity.Execute(x => x.AddItem(new ShoppingCartItem(Guid.NewGuid(), "Foo")));
        entity.Execute(x => x.AddItem(new ShoppingCartItem(Guid.NewGuid(), "Bar")));
        await store.SaveAsync(entity);

        var reloadedEntity = await store
            .LoadAsync<FunctionalEntityWrapper<FunctionalShoppingCart>>(entity.Id.ToString());

        Assert.That(reloadedEntity.Id, Is.EqualTo(entity.Id));
        Assert.That(reloadedEntity.Entity.Items, Is.EqualTo(entity.Entity.Items));
    }

    [Test]
    public async Task WithoutBuilderScenario()
    {
        var streamStoreSettings = new PostgresStreamStoreSettings(ConnectionStrings.PostgresStreamStore);
        var streamStore = new PostgresStreamStore(streamStoreSettings);
        var eventStore = new SqlStreamStoreEventStore(streamStore);

        var entityAdapterRegistry = new EntityAdapterRegistry(
            new Dictionary<Type, IEntityAdapter>(),
            [
                new GenericEntityAdapterFactory(new GenericEntityAdapterTypeResolver(typeof(MutableEntityAdapter<>)))
            ]);

        var eventTypeMappings = new EventTypeMapping[]
        {
            new(typeof(ShoppingSessionStarted)),
            new(typeof(ItemAddedToShoppingCart))
        };

        var serializer = new JsonSerializer();
        var eventMapper = new EventMapper(eventTypeMappings, serializer);

        var config = new EntityStoreConfig(eventStore, entityAdapterRegistry, eventMapper);
        var store = new EntityStore(config);

        var entity = new MutableShoppingCart();
        entity.AddItem(new ShoppingCartItem(Guid.NewGuid(), "Foo"));
        entity.AddItem(new ShoppingCartItem(Guid.NewGuid(), "Bar"));
        await store.SaveAsync(entity);

        var reloadedEntity = await store.LoadAsync<MutableShoppingCart>(entity.Id.ToString());

        Assert.That(reloadedEntity.Id, Is.EqualTo(entity.Id));
        Assert.That(reloadedEntity.Items, Is.EqualTo(entity.Items));
    }

    private static IEnumerable<TestCaseData> TestCases
    {
        get
        {
            yield return new TestCaseData(SqlStreamStoreEntityStore).SetName(nameof(SqlStreamStoreEntityStore));
            yield return new TestCaseData(EventStoreDbEntityStore).SetName(nameof(EventStoreDbEntityStore));
        }
    }
}