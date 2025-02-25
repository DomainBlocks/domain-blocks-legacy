using System.Collections;
using System.Text.Json;
using DomainBlocks.Core.Metadata;
using DomainBlocks.Core.Serialization;
using DomainBlocks.EventStore.Serialization;
using DomainBlocks.Testing;
using EventStore.Client;
using NUnit.Framework;

namespace DomainBlocks.Core.Tests.Serialization;

[TestFixture]
public class MetadataSerializationTests
{
    private const string TestServiceName = "TestService";
    private const string StaticKey = "StaticKey";
    private const string StaticValue = "StaticValue";
    private const string LazyKey = "LazyKey";
    private const string LazyValue = "LazyValue";

    [Test]
    public void DefaultMetadataIsIncludedInEventData()
    {
        var converter = CreateEventConverterWithMetadata();
        var @event = new TestEvent("Some Value");
        var writeEvent = converter.SerializeToWriteEvent(@event);

        var metadata = GetMetadataFromEventPersistenceData(writeEvent);

        Assert.That(metadata.Keys.Count, Is.EqualTo(4));
        AssertDefaultMetadataIsPresent(metadata);
    }

    [Test]
    public void StaticEntriesCanBeAddedToMetadata()
    {
        var converter = CreateEventConverterWithMetadata(context => { context.AddEntry(StaticKey, StaticValue); });

        var @event = new TestEvent("Some Value");
        var persistenceData = converter.SerializeToWriteEvent(@event);

        var metadata = GetMetadataFromEventPersistenceData(persistenceData);

        Assert.That(metadata.Keys.Count, Is.EqualTo(5));
        AssertDefaultMetadataIsPresent(metadata);

        Assert.That(metadata.ContainsKey(StaticKey));
        Assert.That(metadata[StaticKey], Is.EqualTo(StaticValue));
    }

    [Test]
    public void LazyEntriesCanBeAddedToMetadata()
    {
        string LazyValueFunc() => LazyValue;

        var converter = CreateEventConverterWithMetadata(context => { context.AddEntry(LazyKey, LazyValueFunc); });

        var @event = new TestEvent("Some Value");
        var persistenceData = converter.SerializeToWriteEvent(@event);

        var metadata = GetMetadataFromEventPersistenceData(persistenceData);

        Assert.That(metadata.Keys.Count, Is.EqualTo(5));
        AssertDefaultMetadataIsPresent(metadata);

        Assert.That(metadata.ContainsKey(LazyKey));
        Assert.That(metadata[LazyKey], Is.EqualTo(LazyValue));
    }

    [Test]
    [TestCaseSource(typeof(DuplicateKeysData), nameof(DuplicateKeysData.Cases))]
    public bool CannotHaveDuplicateKeys(string[] staticKeys, string[] lazyKeys)
    {
        try
        {
            CreateEventConverterWithMetadata(context =>
            {
                foreach (var key in staticKeys)
                {
                    context.AddEntry(key, "SomeValue");
                }

                foreach (var key in lazyKeys)
                {
                    context.AddEntry(key, () => "LazyValue");
                }
            });
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        return true;
    }

    private class DuplicateKeysData
    {
        public static IEnumerable Cases
        {
            get
            {
                // All keys unique, should not throw and return true
                yield return new TestCaseData(new[] { "Key1", "Key2" }, new[] { "Key3", "Key4" }).Returns(true);
                // Static keys the same. Should throw and return false
                yield return new TestCaseData(new[] { "Key1", "Key1" }, new[] { "Key3", "Key4" }).Returns(false);
                // Lazy keys the same. Should throw and return false
                yield return new TestCaseData(new[] { "Key1", "Key2" }, new[] { "Key3", "Key3" }).Returns(false);
                // Lazy key the same as static key. Should throw and return false
                yield return new TestCaseData(new[] { "Key1", "Key2" }, new[] { "Key2", "Key4" }).Returns(false);
            }
        }
    }

    [Test]
    public void SerializationWorksWhenNoMetadataContext()
    {
        var adapter = new EventStoreEventAdapter(new JsonBytesEventDataSerializer());
        var converter = EventConverter.Create(Fakes.EventNameMap, adapter);

        var @event = new TestEvent("Some Value");
        var writeEvent = converter.SerializeToWriteEvent(@event);

        var metadata = GetMetadataFromEventPersistenceData(writeEvent);

        Assert.That(metadata, Is.Empty);
    }

    [Test]
    public void EmptyMetaDataContextDoesNotIncludeDefaults()
    {
        var emptyMetadataContext = EventMetadataContext.CreateEmpty();
        var adapter = new EventStoreEventAdapter(new JsonBytesEventDataSerializer());
        var converter = EventConverter.Create(Fakes.EventNameMap, adapter, emptyMetadataContext);

        emptyMetadataContext.AddEntry(StaticKey, StaticValue);

        var @event = new TestEvent("Some Value");
        var writeEvent = converter.SerializeToWriteEvent(@event);

        var metadata = GetMetadataFromEventPersistenceData(writeEvent);

        Assert.That(metadata.Keys.Count, Is.EqualTo(1));
        Assert.That(metadata.ContainsKey(StaticKey));
        Assert.That(metadata[StaticKey], Is.EqualTo(StaticValue));
    }

    private static void AssertDefaultMetadataIsPresent(IDictionary<string, string> metadata)
    {
        Assert.That(metadata.ContainsKey(MetadataKeys.LoggedInUserName));
        Assert.That(metadata.ContainsKey(MetadataKeys.MachineName));
        Assert.That(metadata.ContainsKey(MetadataKeys.ServiceName));
        Assert.That(metadata.ContainsKey(MetadataKeys.UtcDateTime));

        Assert.That(metadata[MetadataKeys.LoggedInUserName], Is.EqualTo(Environment.UserName));
        Assert.That(metadata[MetadataKeys.MachineName], Is.EqualTo(Environment.MachineName));
        Assert.That(metadata[MetadataKeys.ServiceName], Is.EqualTo(TestServiceName));
        Assert.That(metadata[MetadataKeys.UtcDateTime], Is.Not.Null);
    }

    private static IEventConverter<ResolvedEvent, EventData> CreateEventConverterWithMetadata(
        Action<EventMetadataContext>? applyCustomMetadata = null)
    {
        var adapter = new EventStoreEventAdapter(new JsonBytesEventDataSerializer());
        var metadataContext = EventMetadataContext.CreateWithDefaults(TestServiceName);
        applyCustomMetadata?.Invoke(metadataContext);
        return EventConverter.Create(Fakes.EventNameMap, adapter, metadataContext);
    }

    private static IDictionary<string, string> GetMetadataFromEventPersistenceData(EventData writeEvent)
    {
        return JsonSerializer.Deserialize<EventMetadata>(writeEvent.Metadata.Span)!;
    }

    private class TestEvent(string value)
    {
        // ReSharper disable once MemberCanBePrivate.Local
        // ReSharper disable once UnusedAutoPropertyAccessor.Local
        public string Value { get; } = value;
    }
}