using DomainBlocks.Core.Builders;
using NUnit.Framework;

namespace DomainBlocks.Core.Tests;

[TestFixture]
public class MutableCommandExecutionContextTests
{
    [Test]
    public void EventEnumerableReturnTypeIsNotEnumeratedAgainWhenMaterializing()
    {
        var builder = new MutableAggregateTypeBuilder<MutableAggregate, IEvent>();
        builder.WithEventEnumerableCommandResult().ApplyEvents(ApplyEventsBehavior.WhileEnumerating);
        builder.ApplyEventsWith((agg, e) => agg.Apply((dynamic)e));
        var aggregateType = builder.Build();

        var aggregate = new MutableAggregate();
        var context = aggregateType.CreateCommandExecutionContext(aggregate);

        // Materialize the results. We expect the comment method to be called only once.
        var events = context.ExecuteCommand(x => x.MyCommandMethod("value")).ToList();

        Assert.That(aggregate.CallCount, Is.EqualTo(1));
        Assert.That(events, Is.EqualTo(context.RaisedEvents));
        Assert.That(context.State, Is.SameAs(aggregate));
        Assert.That(context.State.ObservedValues, Has.Count.EqualTo(3));
        Assert.That(context.State.ObservedValues[0], Is.EqualTo("value 1"));
        Assert.That(context.State.ObservedValues[1], Is.EqualTo("value 1 2"));
        Assert.That(context.State.ObservedValues[2], Is.EqualTo("value 1 2 3"));
    }

    private class MutableAggregate
    {
        public List<string> ObservedValues { get; } = [];
        public int CallCount { get; private set; }
        private string? Value { get; set; }

        public IEnumerable<IEvent> MyCommandMethod(string newValue)
        {
            CallCount++;
            yield return new ValueChangedEvent($"{newValue} 1");
            yield return new ValueChangedEvent($"{Value} 2");
            yield return new ValueChangedEvent($"{Value} 3");
        }

        public void Apply(ValueChangedEvent @event)
        {
            Value = @event.Value;
            ObservedValues.Add(@event.Value);
        }
    }

    private interface IEvent
    {
    }

    private class ValueChangedEvent : IEvent
    {
        public ValueChangedEvent(string value)
        {
            Value = value;
        }

        public string Value { get; }
    }
}