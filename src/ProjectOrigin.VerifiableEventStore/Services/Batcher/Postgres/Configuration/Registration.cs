using Microsoft.Extensions.DependencyInjection;

namespace ProjectOrigin.VerifiableEventStore.Services.Batcher.Postgres.Configuration;

public static class Registration
{
    public static IServiceCollection AddBatchProcessor(this IServiceCollection services)
    {
        services.AddHostedService<BatchProcessorBackgroundService>();
        return services;
    }
}