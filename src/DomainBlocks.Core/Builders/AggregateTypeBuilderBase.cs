using System.Reflection;

namespace DomainBlocks.Core.Builders;

public abstract class AggregateTypeBuilderBase<TAggregate, TEventBase> :
    EventSourcedEntityTypeBuilderBase<TAggregate>,
    IAggregateTypeBuilder
{
    // ReSharper disable once SuggestBaseTypeForParameterInConstructor - we want to ensure the derived type here.
    protected AggregateTypeBuilderBase(AggregateTypeBase<TAggregate, TEventBase> aggregateType) : base(aggregateType)
    {
    }

    internal IAutoAggregateEventTypeBuilder<TAggregate>? AutoEventTypeBuilder { get; set; }

    internal List<IAggregateEventTypeBuilder<TAggregate>> EventTypeBuilders { get; } = [];

    /// <summary>
    /// Finds events deriving from type <see cref="TEventBase"/> in the specified assembly, and adds them to the
    /// aggregate types.
    /// </summary>
    /// <returns>
    /// An object that can be used for further configuration.
    /// </returns>
    public ITypeFilterBuilder UseEventTypesFrom(Assembly assembly)
    {
        var builder = new AssemblyAggregateEventTypeBuilder<TAggregate, TEventBase>(assembly);
        AutoEventTypeBuilder = builder;
        return builder;
    }

    public new IAggregateType Build() => (IAggregateType)base.Build();

    protected override EventSourcedEntityTypeBase<TAggregate> OnBuild(EventSourcedEntityTypeBase<TAggregate> source)
    {
        var aggregateType = (AggregateTypeBase<TAggregate, TEventBase>)source;
        if (AutoEventTypeBuilder != null)
        {
            aggregateType = aggregateType.SetEventTypes(AutoEventTypeBuilder.Build());
        }

        // Any individually configured event types will override auto configured event types for a given CLR type.
        var eventTypes = EventTypeBuilders.Select(x => x.EventType);
        aggregateType = aggregateType.SetEventTypes(eventTypes);
        return aggregateType;
    }
}