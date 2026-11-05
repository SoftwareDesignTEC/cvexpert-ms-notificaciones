using Microsoft.Extensions.Options;
using Notificaciones.Api.Options;
using Notificaciones.Api.Services;

namespace Notificaciones.Api.Infrastructure.Mongo;

/// <summary>
/// Crea al arrancar los índices de <c>notifications</c> y <c>devices</c>, reintentando si Mongo no responde.
/// Sin Mongo configurado no hace nada. El consumidor de la cola espera a que termine (<see cref="WaitUntilReadyAsync"/>):
/// sin el índice único de eventId no hay deduplicación.
/// </summary>
public class MongoIndexInitializer(
    IOptions<MongoOptions> mongoOptions,
    IServiceProvider services,
    ILogger<MongoIndexInitializer> logger) : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(30);

    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task WaitUntilReadyAsync(CancellationToken cancellationToken) => _ready.Task.WaitAsync(cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var mongo = mongoOptions.Value;
        if (string.IsNullOrWhiteSpace(mongo.ConnectionString) || string.IsNullOrWhiteSpace(mongo.Database))
        {
            logger.LogWarning("Falta Mongo:ConnectionString o Mongo:Database: no se crean los índices");
            return;
        }

        while (true)
        {
            try
            {
                await services.GetRequiredService<INotificationRepository>().EnsureIndexesAsync(stoppingToken);
                await services.GetRequiredService<IDeviceRepository>().EnsureIndexesAsync(stoppingToken);
                _ready.TrySetResult();
                logger.LogInformation("Índices de MongoDB listos");
                return;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "No se pudieron crear los índices de MongoDB; se reintenta en {RetryDelay}", RetryDelay);
                await Task.Delay(RetryDelay, stoppingToken);
            }
        }
    }
}
