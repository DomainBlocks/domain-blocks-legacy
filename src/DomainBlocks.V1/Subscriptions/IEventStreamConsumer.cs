using DomainBlocks.V1.Abstractions.Subscriptions;
// ReSharper disable UnusedParameter.Global
// ReSharper disable UnusedMember.Global

namespace DomainBlocks.V1.Subscriptions;

public interface IEventStreamConsumer
{
    string Name => GetType().Name;

    Task OnInitializingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    Task OnSubscribingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    Task OnSubscriptionDroppedAsync(Exception? exception, CancellationToken cancellationToken) => Task.CompletedTask;

    Task OnCaughtUpAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    Task OnFellBehindAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    Task<SubscriptionPosition?> OnRestoreAsync(CancellationToken cancellationToken) =>
        Task.FromResult<SubscriptionPosition?>(null);

    Task OnCheckpointAsync(SubscriptionPosition position, CancellationToken cancellationToken) => Task.CompletedTask;
}