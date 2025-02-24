namespace DomainBlocks.Core.Builders;

public sealed class ModelBuilder
{
    private readonly List<IAggregateTypeBuilder> _aggregateTypeBuilders = [];

    public ModelBuilder Aggregate<TAggregate, TEventBase>(
        Action<MutableAggregateTypeBuilder<TAggregate, TEventBase>> builderAction) where TEventBase : class
    {
        if (builderAction == null) throw new ArgumentNullException(nameof(builderAction));

        var builder = new MutableAggregateTypeBuilder<TAggregate, TEventBase>();
        _aggregateTypeBuilders.Add(builder);
        builderAction(builder);
        return this;
    }

    public ModelBuilder Aggregate<TAggregate>(Action<MutableAggregateTypeBuilder<TAggregate, object>> builderAction)
    {
        return Aggregate<TAggregate, object>(builderAction);
    }

    public ModelBuilder ImmutableAggregate<TAggregate, TEventBase>(
        Action<ImmutableAggregateTypeBuilder<TAggregate, TEventBase>> builderAction) where TEventBase : class
    {
        if (builderAction == null) throw new ArgumentNullException(nameof(builderAction));

        var builder = new ImmutableAggregateTypeBuilder<TAggregate, TEventBase>();
        _aggregateTypeBuilders.Add(builder);
        builderAction(builder);
        return this;
    }

    public ModelBuilder ImmutableAggregate<TAggregate>(
        Action<ImmutableAggregateTypeBuilder<TAggregate, object>> builderAction)
    {
        return ImmutableAggregate<TAggregate, object>(builderAction);
    }

    public Model Build()
    {
        var aggregateTypes = _aggregateTypeBuilders.Select(x => x.Build());
        return new Model(aggregateTypes);
    }
}