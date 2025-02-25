using DomainBlocks.V1.Abstractions.Exceptions;

namespace DomainBlocks.V1.Persistence;

public interface IEntityStore
{
    /// <summary>
    /// Loads an entity from an event stream in the event store. <br/>
    /// If the stream does not exist, a <see cref="StreamNotFoundException"/> is thrown.
    /// </summary>
    /// <param name="entityId">The ID of the entity</param>
    /// <param name="cancellationToken">A cancellation token</param>
    /// <typeparam name="TEntity">The type of the entity</typeparam>
    /// <returns>An typed entity instance</returns>
    Task<TEntity> LoadAsync<TEntity>(string entityId, CancellationToken cancellationToken = default)
        where TEntity : notnull;

    /// <summary>
    /// Creates or loads an entity from an event stream in the event store. <br/>
    /// If the stream does not exist, a new entity is created.
    /// </summary>
    /// <param name="entityId">The ID of the entity</param>
    /// <param name="cancellationToken">A cancellation token</param>
    /// <typeparam name="TEntity">The type of the entity</typeparam>
    /// <returns>An typed entity instance</returns>
    Task<TEntity> LoadOrCreateAsync<TEntity>(string entityId,
        CancellationToken cancellationToken = default)
        where TEntity : notnull;

    Task SaveAsync<TEntity>(TEntity entity, CancellationToken cancellationToken = default) where TEntity : notnull;
}