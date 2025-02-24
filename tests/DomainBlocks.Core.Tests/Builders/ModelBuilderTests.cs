using DomainBlocks.Core.Builders;
using NUnit.Framework;

namespace DomainBlocks.Core.Tests.Builders;

[TestFixture]
public class ModelBuilderTests
{
    [Test]
    public void MutableAggregateWithEventsPropertyScenario()
    {
        var model = new ModelBuilder()
            .Aggregate<MutableAggregate, IEvent>(builder => builder.WithRaisedEventsFrom(x => x.RaisedEvents))
            .Build();

        var aggregateType = model.GetAggregateType<MutableAggregate>();
        var aggregate = new MutableAggregate();
        var context = aggregateType.CreateCommandExecutionContext(aggregate);

        context.ExecuteCommand(x => x.ChangeValueWithEventsProperty("value"));
        var events = context.RaisedEvents.ToList();

        Assert.That(context.State, Is.SameAs(aggregate));
        Assert.That(context.State.Value, Is.EqualTo("value 3"));
        Assert.That(events, Has.Count.EqualTo(3));
        Assert.That(events[0], Is.EqualTo(new ValueChangedEvent("value 1")));
        Assert.That(events[1], Is.EqualTo(new ValueChangedEvent("value 2")));
        Assert.That(events[2], Is.EqualTo(new ValueChangedEvent("value 3")));
    }

    [Test]
    public void MutableAggregateApplyAfterEnumeratingScenario()
    {
        var model = new ModelBuilder()
            .Aggregate<MutableAggregate, IEvent>(builder =>
            {
                builder.WithEventEnumerableCommandResult().ApplyEvents();
                builder.Event<ValueChangedEvent>().ApplyWith((a, e) => a.Apply(e));
            })
            .Build();

        var aggregateType = model.GetAggregateType<MutableAggregate>();
        var aggregate = new MutableAggregate();
        var context = aggregateType.CreateCommandExecutionContext(aggregate);

        context.ExecuteCommand(x => x.ChangeValueWithYieldReturnedEvents("value"));
        var events = context.RaisedEvents.ToList();

        Assert.That(context.State, Is.SameAs(aggregate));
        Assert.That(context.State.Value, Is.EqualTo("value 3"));
        Assert.That(events, Has.Count.EqualTo(3));
        Assert.That(events[0], Is.EqualTo(new ValueChangedEvent("value 1")));
        Assert.That(events[1], Is.EqualTo(new ValueChangedEvent("value 2")));
        Assert.That(events[2], Is.EqualTo(new ValueChangedEvent("value 3")));
    }

    [Test]
    public void MutableAggregateApplyWhileEnumeratingScenario()
    {
        var model = new ModelBuilder()
            .Aggregate<MutableAggregate, IEvent>(builder =>
            {
                builder
                    .WithEventEnumerableCommandResult()
                    .ApplyEvents(ApplyEventsBehavior.WhileEnumerating);

                builder.Event<ValueChangedEvent>().ApplyWith((a, e) => a.Apply(e));
            })
            .Build();

        var aggregateType = model.GetAggregateType<MutableAggregate>();
        var aggregate = new MutableAggregate();
        var context = aggregateType.CreateCommandExecutionContext(aggregate);

        var result = context.ExecuteCommand(x => x.ChangeValueWithYieldReturnedEvents("value"));
        var events = context.RaisedEvents.ToList();

        Assert.That(context.State, Is.SameAs(aggregate));
        Assert.That(context.State.Value, Is.EqualTo("value 1 2 3"));
        Assert.That(context.RaisedEvents, Is.EqualTo(result));
        Assert.That(events, Has.Count.EqualTo(3));
        Assert.That(events[0], Is.EqualTo(new ValueChangedEvent("value 1")));
        Assert.That(events[1], Is.EqualTo(new ValueChangedEvent("value 1 2")));
        Assert.That(events[2], Is.EqualTo(new ValueChangedEvent("value 1 2 3")));
    }

    [Test]
    public void MutableAggregateWithCommandResultAndEventsAppliedScenario()
    {
        var model = new ModelBuilder()
            .Aggregate<MutableAggregate, IEvent>(builder =>
            {
                builder.CommandResult<CommandResult>().WithEventsFrom(x => x.Events);
            })
            .Build();

        var aggregateType = model.GetAggregateType<MutableAggregate>();
        var aggregate = new MutableAggregate();
        var context = aggregateType.CreateCommandExecutionContext(aggregate);

        var result = context.ExecuteCommand(x => x.ChangeValueWithCommandResultAndEventsApplied("value"));
        var events = context.RaisedEvents.ToList();

        Assert.That(context.State, Is.SameAs(aggregate));
        Assert.That(context.State.Value, Is.EqualTo("value 3"));
        Assert.That(context.RaisedEvents, Is.EqualTo(result.Events));
        Assert.That(events, Has.Count.EqualTo(3));
        Assert.That(events[0], Is.EqualTo(new ValueChangedEvent("value 1")));
        Assert.That(events[1], Is.EqualTo(new ValueChangedEvent("value 2")));
        Assert.That(events[2], Is.EqualTo(new ValueChangedEvent("value 3")));
    }

    [Test]
    public void MutableAggregateWithCommandResultAndEventsNotAppliedScenario()
    {
        var model = new ModelBuilder()
            .Aggregate<MutableAggregate, IEvent>(builder =>
            {
                builder
                    .CommandResult<CommandResult>()
                    .WithEventsFrom(x => x.Events)
                    .ApplyEvents();

                builder.Event<ValueChangedEvent>().ApplyWith((a, e) => a.Apply(e));
            })
            .Build();

        var aggregateType = model.GetAggregateType<MutableAggregate>();
        var aggregate = new MutableAggregate();
        var context = aggregateType.CreateCommandExecutionContext(aggregate);

        var result = context.ExecuteCommand(x => x.ChangeValueWithCommandResultAndEventsNotApplied("value"));
        var events = context.RaisedEvents.ToList();

        Assert.That(context.State, Is.SameAs(aggregate));
        Assert.That(context.State.Value, Is.EqualTo("value 3"));
        Assert.That(context.RaisedEvents, Is.EqualTo(result.Events));
        Assert.That(events, Has.Count.EqualTo(3));
        Assert.That(events[0], Is.EqualTo(new ValueChangedEvent("value 1")));
        Assert.That(events[1], Is.EqualTo(new ValueChangedEvent("value 2")));
        Assert.That(events[2], Is.EqualTo(new ValueChangedEvent("value 3")));
    }

    [Test]
    public void MutableAggregateWithAutoConfiguredEventsScenario()
    {
        var model = new ModelBuilder()
            .Aggregate<MutableAggregate, IEvent>(builder => builder.AutoConfigureEvents())
            .Build();

        var aggregateType = model.GetAggregateType<MutableAggregate>();
        var aggregate = new MutableAggregate();
        var context = aggregateType.CreateCommandExecutionContext(aggregate);

        context.ExecuteCommand(x => x.ChangeValueWithYieldReturnedEvents("value"));
        var updatedAggregate = context.RaisedEvents.Aggregate(aggregate, aggregateType.InvokeEventApplier);

        Assert.That(updatedAggregate, Is.SameAs(aggregate));
        Assert.That(updatedAggregate.Value, Is.EqualTo("value 3"));
    }

    [Test]
    public void ImmutableAggregateDefaultScenario()
    {
        var model = new ModelBuilder()
            .ImmutableAggregate<ImmutableAggregate, IEvent>(builder =>
            {
                builder.Event<ValueChangedEvent>().ApplyWith((a, e) => a.Apply(e));
            })
            .Build();

        var aggregateType = model.GetAggregateType<ImmutableAggregate>();
        var aggregate = new ImmutableAggregate();
        var context = aggregateType.CreateCommandExecutionContext(aggregate);

        var result = context.ExecuteCommand(x => x.ChangeValueWithYieldReturnedEvents("value"));
        var events = context.RaisedEvents.ToList();

        Assert.That(context.State, Is.Not.SameAs(aggregate));
        Assert.That(context.State.Value, Is.EqualTo("value 3"));
        Assert.That(context.RaisedEvents, Is.EqualTo(result));
        Assert.That(events, Has.Count.EqualTo(3));
        Assert.That(events[0], Is.EqualTo(new ValueChangedEvent("value 1")));
        Assert.That(events[1], Is.EqualTo(new ValueChangedEvent("value 2")));
        Assert.That(events[2], Is.EqualTo(new ValueChangedEvent("value 3")));
    }

    [Test]
    public void ImmutableAggregateWithCommandResultAndEventsAppliedScenario()
    {
        var model = new ModelBuilder()
            .ImmutableAggregate<ImmutableAggregate, IEvent>(builder =>
            {
                builder
                    .CommandResult<CommandResult>()
                    .WithEventsFrom(x => x.Events)
                    .WithUpdatedStateFrom(x => x.UpdatedState);
            })
            .Build();

        var aggregateType = model.GetAggregateType<ImmutableAggregate>();
        var aggregate = new ImmutableAggregate();
        var context = aggregateType.CreateCommandExecutionContext(aggregate);

        var result = context.ExecuteCommand(x => x.ChangeValueWithCommandResultAndEventsApplied("value"));
        var events = context.RaisedEvents.ToList();

        Assert.That(context.State, Is.Not.SameAs(aggregate));
        Assert.That(context.State.Value, Is.EqualTo("value 1 2 3"));
        Assert.That(context.RaisedEvents, Is.EqualTo(result.Events));
        Assert.That(events, Has.Count.EqualTo(3));
        Assert.That(events[0], Is.EqualTo(new ValueChangedEvent("value 1")));
        Assert.That(events[1], Is.EqualTo(new ValueChangedEvent("value 1 2")));
        Assert.That(events[2], Is.EqualTo(new ValueChangedEvent("value 1 2 3")));
    }

    [Test]
    public void ImmutableAggregateWithCommandResultAndEventsNotAppliedScenario()
    {
        var model = new ModelBuilder()
            .ImmutableAggregate<ImmutableAggregate, IEvent>(builder =>
            {
                builder
                    .CommandResult<CommandResult>()
                    .WithEventsFrom(x => x.Events);

                builder.Event<ValueChangedEvent>().ApplyWith((a, e) => a.Apply(e));
            })
            .Build();

        var aggregateType = model.GetAggregateType<ImmutableAggregate>();
        var aggregate = new ImmutableAggregate();
        var context = aggregateType.CreateCommandExecutionContext(aggregate);

        var result = context.ExecuteCommand(x => x.ChangeValueWithCommandResultAndEventsNotApplied("value"));
        var events = context.RaisedEvents.ToList();

        Assert.That(context.State, Is.Not.SameAs(aggregate));
        Assert.That(context.State.Value, Is.EqualTo("value 3"));
        Assert.That(context.RaisedEvents, Is.EqualTo(result.Events));
        Assert.That(events, Has.Count.EqualTo(3));
        Assert.That(events[0], Is.EqualTo(new ValueChangedEvent("value 1")));
        Assert.That(events[1], Is.EqualTo(new ValueChangedEvent("value 2")));
        Assert.That(events[2], Is.EqualTo(new ValueChangedEvent("value 3")));
    }

    [Test]
    public void ImmutableAggregateWithAutoConfiguredEventsScenario()
    {
        var model = new ModelBuilder()
            .ImmutableAggregate<ImmutableAggregate, IEvent>(builder => builder.AutoConfigureEvents())
            .Build();

        var aggregateType = model.GetAggregateType<ImmutableAggregate>();
        var aggregate = new ImmutableAggregate();
        var context = aggregateType.CreateCommandExecutionContext(aggregate);

        context.ExecuteCommand(x => x.ChangeValueWithYieldReturnedEvents("value"));
        var updatedAggregate = context.RaisedEvents.Aggregate(aggregate, aggregateType.InvokeEventApplier);

        Assert.That(updatedAggregate, Is.Not.SameAs(aggregate));
        Assert.That(updatedAggregate.Value, Is.EqualTo("value 3"));
    }

    private interface IEvent
    {
    }

    private record ValueChangedEvent(string Value) : IEvent;

    private class CommandResult
    {
        public IReadOnlyCollection<IEvent> Events { get; init; } = null!;
        public ImmutableAggregate UpdatedState { get; init; } = null!;
    }

    private class MutableAggregate
    {
        private readonly List<IEvent> _raisedEvents = [];

        public string? Value { get; private set; }
        public IReadOnlyCollection<IEvent> RaisedEvents => _raisedEvents;

        public IEnumerable<IEvent> ChangeValueWithYieldReturnedEvents(string newValue)
        {
            yield return new ValueChangedEvent($"{newValue} 1");

            // Using the Value property if it's not null allows us to observe state changes between yielding events
            // when the event enumeration mode is ApplyWhileEnumerating.
            yield return new ValueChangedEvent($"{Value ?? newValue} 2");
            yield return new ValueChangedEvent($"{Value ?? newValue} 3");
        }

        public void ChangeValueWithEventsProperty(string newValue)
        {
            var event1 = new ValueChangedEvent($"{newValue} 1");
            var event2 = new ValueChangedEvent($"{newValue} 2");
            var event3 = new ValueChangedEvent($"{newValue} 3");

            Apply(event1);
            Apply(event2);
            Apply(event3);

            _raisedEvents.Add(event1);
            _raisedEvents.Add(event2);
            _raisedEvents.Add(event3);
        }

        public CommandResult ChangeValueWithCommandResultAndEventsApplied(string newValue)
        {
            var event1 = new ValueChangedEvent($"{newValue} 1");
            var event2 = new ValueChangedEvent($"{newValue} 2");
            var event3 = new ValueChangedEvent($"{newValue} 3");

            Apply(event1);
            Apply(event2);
            Apply(event3);

            return new CommandResult { Events = [event1, event2, event3] };
        }

        public CommandResult ChangeValueWithCommandResultAndEventsNotApplied(string newValue)
        {
            var event1 = new ValueChangedEvent($"{newValue} 1");
            var event2 = new ValueChangedEvent($"{newValue} 2");
            var event3 = new ValueChangedEvent($"{newValue} 3");

            return new CommandResult { Events = [event1, event2, event3] };
        }

        public void Apply(ValueChangedEvent e)
        {
            Value = e.Value;
        }
    }

    private class ImmutableAggregate
    {
        public ImmutableAggregate()
        {
        }

        private ImmutableAggregate(string value)
        {
            Value = value;
        }

        public string? Value { get; }

        public IEnumerable<IEvent> ChangeValueWithYieldReturnedEvents(string newValue)
        {
            yield return new ValueChangedEvent($"{newValue} 1");
            yield return new ValueChangedEvent($"{newValue} 2");
            yield return new ValueChangedEvent($"{newValue} 3");
        }

        public CommandResult ChangeValueWithCommandResultAndEventsApplied(string newValue)
        {
            var event1 = new ValueChangedEvent($"{newValue} 1");
            var state = Apply(event1);

            // Here we show that the intermediate states can be observed manually, in the same way as they would be
            // if using a mutable event with ApplyWhileEnumerating, as in the above scenario.
            var event2 = new ValueChangedEvent($"{state.Value} 2");
            state = state.Apply(event2);

            var event3 = new ValueChangedEvent($"{state.Value} 3");
            state = state.Apply(event3);

            return new CommandResult { Events = [event1, event2, event3], UpdatedState = state };
        }

        public CommandResult ChangeValueWithCommandResultAndEventsNotApplied(string newValue)
        {
            var event1 = new ValueChangedEvent($"{newValue} 1");
            var event2 = new ValueChangedEvent($"{newValue} 2");
            var event3 = new ValueChangedEvent($"{newValue} 3");

            return new CommandResult { Events = [event1, event2, event3] };
        }

        public ImmutableAggregate Apply(ValueChangedEvent @event)
        {
            return new ImmutableAggregate(@event.Value);
        }
    }
}