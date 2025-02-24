using NUnit.Framework;

namespace DomainBlocks.Core.Tests;

[TestFixture]
public class AggregateEventTypeTests
{
    [Test]
    public void MergeMergesValuesCorrectly()
    {
        var eventType1 = new AggregateEventType<MyAggregate, IEvent, MyEvent>()
            .HideGenericType();

        var eventType2 = new AggregateEventType<MyAggregate, IEvent, MyEvent>()
            .SetEventApplier((agg, e) => agg.Apply(e))
            .HideGenericType();

        var eventType3 = new AggregateEventType<MyAggregate, IEvent, MyEvent>()
            .SetEventName("MyEvent2")
            .HideGenericType();

        var result = eventType1
            .Merge(eventType2)
            .Merge(eventType3);

        var aggregate = new MyAggregate();
        result.InvokeEventApplier(aggregate, new MyEvent());

        Assert.That(result.ClrType, Is.EqualTo(typeof(MyEvent)));
        Assert.That(aggregate.IsEventApplied, Is.True);
        Assert.That(result.EventName, Is.EqualTo("MyEvent2"));
    }

    private class MyAggregate
    {
        public bool IsEventApplied { get; private set; }

        public void Apply(MyEvent _)
        {
            IsEventApplied = true;
        }
    }

    private interface IEvent;

    private class MyEvent : IEvent;
}