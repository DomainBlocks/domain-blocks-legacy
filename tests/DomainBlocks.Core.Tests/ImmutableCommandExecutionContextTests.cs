using System.Collections.Immutable;
using DomainBlocks.Core.Builders;
using NUnit.Framework;

namespace DomainBlocks.Core.Tests;

[TestFixture]
public class ImmutableCommandExecutionContextTests
{
    [Test]
    public void EventEnumerableReturnTypeIsNotEnumeratedAgainWhenMaterializing()
    {
        var builder = new ImmutableAggregateTypeBuilder<ImmutableAggregate, IEvent>();
        builder.ApplyEventsWith((agg, e) => agg.Apply((dynamic)e));
        var aggregateType = builder.Build();

        var aggregate = new ImmutableAggregate();
        var context = aggregateType.CreateCommandExecutionContext(aggregate);

        // Materialize the results. We expect the comment method to be called only once.
        var events = context.ExecuteCommand(x => x.MyCommandMethod("value")).ToList();

        Assert.That(ImmutableAggregate.CallCount, Is.EqualTo(1));
        Assert.That(events, Is.EqualTo(context.RaisedEvents));
        // We expect the state to be different, since this is immutable.
        Assert.That(context.State, Is.Not.SameAs(aggregate));
        Assert.That(context.State.ObservedValues, Has.Count.EqualTo(3));
        Assert.That(context.State.ObservedValues[0], Is.EqualTo("value 1"));
        Assert.That(context.State.ObservedValues[1], Is.EqualTo("value 2"));
        Assert.That(context.State.ObservedValues[2], Is.EqualTo("value 3"));
    }

    private class ImmutableAggregate
    {
        public ImmutableAggregate()
        {
        }

        private ImmutableAggregate(ImmutableList<string> observedValues)
        {
            ObservedValues = observedValues;
        }

        public ImmutableList<string> ObservedValues { get; } = ImmutableList<string>.Empty;

        // This is a bit of a hack, but we keep a static property here to keep track of the call count, since this is
        // an immutable type.
        public static int CallCount { get; private set; }

        public IEnumerable<IEvent> MyCommandMethod(string newValue)
        {
            CallCount++;
            yield return new ValueChangedEvent($"{newValue} 1");
            yield return new ValueChangedEvent($"{newValue} 2");
            yield return new ValueChangedEvent($"{newValue} 3");
        }

        public ImmutableAggregate Apply(ValueChangedEvent @event)
        {
            return new ImmutableAggregate(ObservedValues.Add(@event.Value));
        }
    }

    private interface IEvent;

    private class ValueChangedEvent : IEvent
    {
        public ValueChangedEvent(string value)
        {
            Value = value;
        }

        public string Value { get; }
    }
}