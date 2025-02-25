using NUnit.Framework;

namespace DomainBlocks.Core.Tests;

[TestFixture]
public class AggregateTypeTests
{
    [Test]
    public void CreateNewUsesDefaultConstructorWhenNoFactorySpecified()
    {
        var aggregateType = new MutableAggregateType<MyAggregate, object>();
        var aggregate = aggregateType.CreateNew();
        Assert.That(aggregate, Is.Not.Null);
    }

    [Test]
    public void CreateNewThrowsInvalidOperationWhenNoDefaultConstructorAndMissingFactory()
    {
        var aggregateType = new MutableAggregateType<MyAggregateWithNoPublicDefaultCtor, object>();
        Assert.Throws<InvalidOperationException>(() => aggregateType.CreateNew());
    }

    [Test]
    public void MakeSnapshotKeyUsesDefaultIdSelectorWhenIdPropertyExists()
    {
        var aggregateType1 = new MutableAggregateType<MyAggregateWithId1, object>();
        var aggregateType2 = new MutableAggregateType<MyAggregateWithId2, object>();
        var aggregate1 = new MyAggregateWithId1 { Id = 123 };
        var aggregate2 = new MyAggregateWithId2 { MyAggregateWithId2Id = 456 };
        Assert.That(aggregateType1.MakeSnapshotKey(aggregate1), Is.EqualTo("myAggregateWithId1Snapshot-123"));
        Assert.That(aggregateType2.MakeSnapshotKey(aggregate2), Is.EqualTo("myAggregateWithId2Snapshot-456"));
    }

    [Test]
    public void MakeSnapshotKeyThrowsWhenMissingIdSelectorAndNoIdPropertyExists()
    {
        var aggregateType = new MutableAggregateType<MyAggregate, object>();
        Assert.Throws<InvalidOperationException>(() => aggregateType.MakeSnapshotKey(new MyAggregate()));
    }

    [Test]
    public void MakeStreamKeyUsesDefaultSelectorWhenNotSpecified()
    {
        var aggregateType = new MutableAggregateType<MyAggregate, object>();
        var streamKey = aggregateType.MakeStreamKey("1");
        Assert.That(streamKey, Is.EqualTo("myAggregate-1"));
    }

    [Test]
    public void MakeSnapshotKeyUsesDefaultSelectorWhenNotSpecified()
    {
        var aggregateType = new MutableAggregateType<MyAggregate, object>();
        var snapshotKey = aggregateType.MakeSnapshotKey("1");
        Assert.That(snapshotKey, Is.EqualTo("myAggregateSnapshot-1"));
    }

    [Test]
    public void KeySelectorsUsesPrefixWhenSpecified()
    {
        var aggregateType = new MutableAggregateType<MyAggregate, object>().SetKeyPrefix("myPrefix");
        Assert.That(aggregateType.MakeStreamKey("1"), Is.EqualTo("myPrefix-1"));
        Assert.That(aggregateType.MakeSnapshotKey("1"), Is.EqualTo("myPrefixSnapshot-1"));
    }

    private class MyAggregate;

    // ReSharper disable UnusedAutoPropertyAccessor.Local
    private class MyAggregateWithId1
    {
        public int Id { get; init; }
    }

    private class MyAggregateWithId2
    {
        public int MyAggregateWithId2Id { get; init; }
    }
    // ReSharper restore UnusedAutoPropertyAccessor.Local

    // ReSharper disable once ClassNeverInstantiated.Local
    private class MyAggregateWithNoPublicDefaultCtor
    {
        private MyAggregateWithNoPublicDefaultCtor()
        {
        }
    }
}