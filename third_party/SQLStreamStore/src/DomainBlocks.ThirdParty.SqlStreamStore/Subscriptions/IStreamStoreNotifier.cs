using DomainBlocks.ThirdParty.SqlStreamStore.Infrastructure;

namespace DomainBlocks.ThirdParty.SqlStreamStore.Subscriptions
{
    /// <summary>
    ///     Represents an notifier lets subsribers know that the 
    ///     stream store has new messages.
    /// </summary>
    public interface IStreamStoreNotifier : IObservable<Unit>, IDisposable;
}