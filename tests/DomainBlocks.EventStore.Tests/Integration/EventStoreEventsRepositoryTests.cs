using System.Text;
using DomainBlocks.Core.Persistence;
using DomainBlocks.Core.Serialization;
using DomainBlocks.EventStore.Persistence;
using DomainBlocks.EventStore.Serialization;
using DomainBlocks.EventStore.Testing;
using DomainBlocks.Testing;
using EventStore.Client;
using NUnit.Framework;

namespace DomainBlocks.EventStore.Tests.Integration;

[TestFixture]
public class EventStoreEventsRepositoryTests : EventStoreIntegrationTest
{
    [TestCase(2, Description = "Small batch that can be saved in one round trip")]
    [TestCase(5000, Description = "Large batch that will need a transaction")]
    public async Task EventsCanBeSaved(int eventCount)
    {
        var repo = CreateRepository();
        var nextVersion = await repo.SaveEventsAsync(EventStoreTestHelpers.RandomStreamName(),
            StreamVersion.NewStream,
            EventStoreTestHelpers.GenerateTestEvents(eventCount));

        Assert.That(nextVersion, Is.EqualTo(eventCount - 1));
    }

    [Test]
    public void ArgumentNullExceptionIsThrownWhenEventArrayIsNullOnSave()
    {
        Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            var repo = CreateRepository();
            await repo.SaveEventsAsync(EventStoreTestHelpers.RandomStreamName(), StreamVersion.NewStream, null!);
        });
    }

    [Test]
    public void ArgumentNullExceptionIsThrownWhenStreamIsNullOnSave()
    {
        Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            var repo = CreateRepository();
            await repo.SaveEventsAsync(null!,
                StreamVersion.NewStream,
                EventStoreTestHelpers.GenerateTestEvents(2));
        });
    }

    [Test]
    public async Task EmptyEventArrayDoesNotCauseExceptionOnSave()
    {
        var repo = CreateRepository();
        var nextVersion = await repo.SaveEventsAsync(EventStoreTestHelpers.RandomStreamName(),
            StreamVersion.NewStream,
            new TestEvent[] { });

        Assert.That(nextVersion, Is.EqualTo(-1));
    }

    [TestCase(2, Description = "Small batch that can be read in one round trip")]
    [TestCase(5000, Description = "Large batch that will need multiple reads")]
    public async Task EventsCanBeLoaded(int eventCount)
    {
        var repo = CreateRepository();
        var streamName = EventStoreTestHelpers.RandomStreamName();
        await repo.SaveEventsAsync(streamName,
            StreamVersion.NewStream,
            EventStoreTestHelpers.GenerateTestEvents(eventCount));

        var events = await repo.LoadEventsAsync(streamName).Cast<TestEvent>().ToListAsync();

        Assert.That(events.Count, Is.EqualTo(eventCount));

        for (var i = 0; i < eventCount; i++)
        {
            Assert.That(events[i].Number, Is.EqualTo(i + 1));
        }
    }

    [Test]
    // TODO (DS): We no longer have a callback, as we weren't using this anywhere and it meant that the raw type needed
    // to be exposed on the interface. We need to consider what strategy we'd like to use for this scenario.
    public async Task EventDeserializeExceptionIsThrownOnBadEventData()
    {
        var repo = CreateRepository();
        var streamName = EventStoreTestHelpers.RandomStreamName();
        var events = EventStoreTestHelpers.GenerateTestEvents(2);
        var version = await repo.SaveEventsAsync(streamName, StreamVersion.NewStream, events);

        const string badEventDataString = "This is not JSON. It should fail on deserialize";
        var badEventData = new EventData(Uuid.NewUuid(), nameof(TestEvent), Encoding.UTF8.GetBytes(badEventDataString));

        var writeResult = await EventStoreClient.AppendToStreamAsync(
            streamName,
            StreamRevision.FromInt64(version),
            [badEventData]);

        var nextStreamRevision = writeResult.NextExpectedStreamRevision;

        // If the bad event data is saved, the version should increment
        var expectedStreamRevision = StreamRevision.FromInt64(version + 1);
        Assert.That(nextStreamRevision, Is.EqualTo(expectedStreamRevision));

        Assert.ThrowsAsync<EventDeserializeException>(async () => await repo.LoadEventsAsync(streamName).ToListAsync());
    }

    [Test]
    public void ArgumentNullExceptionIsThrownWhenStreamIsNullOnLoad()
    {
        Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            var repo = CreateRepository();
            await repo.LoadEventsAsync(null!).ToListAsync();
        });
    }

    private IEventsRepository CreateRepository()
    {
        var serializer = new JsonBytesEventDataSerializer();
        var adapter = new EventStoreEventAdapter(serializer);
        var converter = EventConverter.Create(Fakes.EventNameMap, adapter);
        return new EventStoreEventsRepository(EventStoreClient, converter);
    }
}