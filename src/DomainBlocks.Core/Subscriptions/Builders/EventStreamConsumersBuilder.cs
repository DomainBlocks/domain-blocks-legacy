using DomainBlocks.Core.Serialization;
using DomainBlocks.Core.Subscriptions.Extensions;

namespace DomainBlocks.Core.Subscriptions.Builders;

public class EventStreamConsumersBuilder<TEvent, TPosition> :
    IEventStreamConsumerBuilderInfrastructure<TEvent, TPosition>
    where TPosition : struct, IEquatable<TPosition>, IComparable<TPosition>
{
    private readonly List<IEventStreamConsumerBuilder<TEvent, TPosition>> _consumerBuilders = [];
    private readonly List<IEventStreamInterceptor<TEvent, TPosition>> _interceptors = [];

    public EventStreamConsumersBuilder(EventStreamSubscriptionBuilder coreBuilder)
    {
        CoreBuilder = coreBuilder;
    }

    public EventStreamSubscriptionBuilder CoreBuilder { get; }

    public EventStreamConsumersBuilder<TEvent, TPosition> AddInterceptors(
        IEnumerable<IEventStreamInterceptor<TEvent, TPosition>> interceptors)
    {
        _interceptors.AddRange(interceptors);
        return this;
    }

    public EventStreamConsumersBuilder<TEvent, TPosition> AddInterceptors(
        params IEventStreamInterceptor<TEvent, TPosition>[] interceptors)
    {
        _interceptors.AddRange(interceptors);
        return this;
    }

    void IEventStreamConsumerBuilderInfrastructure<TEvent, TPosition>.AddConsumerBuilder(
        IEventStreamConsumerBuilder<TEvent, TPosition> builder)
    {
        _consumerBuilders.Add(builder);
    }

    IEnumerable<IEventStreamConsumer<TEvent, TPosition>>
        IEventStreamConsumerBuilderInfrastructure<TEvent, TPosition>.Build(IReadEventAdapter<TEvent> readEventAdapter)
    {
        var subscribers = _consumerBuilders.Select(x => x.Build(readEventAdapter).Intercept(_interceptors));
        return subscribers;
    }
}