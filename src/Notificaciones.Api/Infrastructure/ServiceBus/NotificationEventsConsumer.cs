using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;
using Notificaciones.Api.Infrastructure.Mongo;
using Notificaciones.Api.Options;
using Notificaciones.Api.Services.Events;

namespace Notificaciones.Api.Infrastructure.ServiceBus;

/// <summary>
/// Consume la cola de eventos con un <see cref="ServiceBusProcessor"/> (PeekLock, sin completado automático).
/// Si falta configuración de Service Bus o de Mongo no arranca, pero la API sigue funcionando.
/// </summary>
public class NotificationEventsConsumer(
    IOptions<ServiceBusOptions> serviceBusOptions,
    IOptions<MongoOptions> mongoOptions,
    MongoIndexInitializer indexes,
    IServiceProvider services,
    ILogger<NotificationEventsConsumer> logger) : BackgroundService
{
    private const int MaxConcurrentCalls = 4;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var serviceBus = serviceBusOptions.Value;
        var mongo = mongoOptions.Value;

        if (string.IsNullOrWhiteSpace(serviceBus.ConnectionString) || string.IsNullOrWhiteSpace(serviceBus.QueueName))
        {
            logger.LogWarning("Falta ServiceBus:ConnectionString o ServiceBus:QueueName: el consumidor de la cola no se inicia");
            return;
        }

        if (string.IsNullOrWhiteSpace(mongo.ConnectionString) || string.IsNullOrWhiteSpace(mongo.Database))
        {
            logger.LogWarning("Falta Mongo:ConnectionString o Mongo:Database: el consumidor de la cola no se inicia");
            return;
        }

        // Un error aquí (por ejemplo, una cadena mal formada) no debe tumbar la API.
        try
        {
            // Sin el índice único de eventId no hay deduplicación: no se consume hasta tenerlo.
            await indexes.WaitUntilReadyAsync(stoppingToken);

            var handler = services.GetRequiredService<NotificationEventHandler>();
            await using var client = new ServiceBusClient(serviceBus.ConnectionString);
            await using var processor = client.CreateProcessor(serviceBus.QueueName, new ServiceBusProcessorOptions
            {
                ReceiveMode = ServiceBusReceiveMode.PeekLock,
                AutoCompleteMessages = false,
                MaxConcurrentCalls = MaxConcurrentCalls,
            });

            processor.ProcessMessageAsync += args => handler.HandleAsync(new ServiceBusQueueMessage(args), args.CancellationToken);
            processor.ProcessErrorAsync += args =>
            {
                logger.LogError(args.Exception, "Error de Service Bus ({ErrorSource}) en {EntityPath}", args.ErrorSource, args.EntityPath);
                return Task.CompletedTask;
            };

            await processor.StartProcessingAsync(stoppingToken);
            logger.LogInformation("Consumiendo la cola {QueueName}", serviceBus.QueueName);

            try
            {
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
            }

            await processor.StopProcessingAsync(CancellationToken.None);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "El consumidor de la cola se detuvo por un error; la API sigue funcionando");
        }
    }
}
