using EventStore.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DomainBlocks.EventStore;

public static class EventStoreServiceCollectionExtensions
{
    private static readonly Lock SyncLock = new();
    private static EventStoreClient? _eventStoreClient;

    public static IServiceCollection AddEventStore(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<EventStoreConnectionOptions>()
            .Bind(configuration.GetSection(EventStoreConnectionOptions.ConfigSection))
            .ValidateDataAnnotations();

        services.AddSingleton(provider =>
        {
            if (_eventStoreClient == null)
            {
                lock (SyncLock)
                {
                    if (_eventStoreClient == null)
                    {
                        var options = provider.GetRequiredService<IOptions<EventStoreConnectionOptions>>();

                        var settings = EventStoreClientSettings.Create(options.Value.ConnectionString);
                        _eventStoreClient = new EventStoreClient(settings);
                    }
                }
            }

            return _eventStoreClient;
        });

        return services;
    }
}