using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Notificaciones.Api.Infrastructure.ServiceBus;
using Notificaciones.Api.Options;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Notificaciones.Tests;

public class NotificationEventsConsumerTests
{
    [Theory]
    [InlineData("", "notificaciones-eventos", "mongodb://localhost", "notificaciones")]
    [InlineData("Endpoint=sb://ejemplo.servicebus.windows.net/", "", "mongodb://localhost", "notificaciones")]
    [InlineData("Endpoint=sb://ejemplo.servicebus.windows.net/", "notificaciones-eventos", "", "notificaciones")]
    public async Task SinConfiguracion_NoArrancaYTerminaSinError(string serviceBus, string cola, string mongo, string baseDeDatos)
    {
        // Proveedor vacío: si el consumidor intentara usar Mongo o el handler, fallaría.
        var consumidor = new NotificationEventsConsumer(
            MsOptions.Create(new ServiceBusOptions { ConnectionString = serviceBus, QueueName = cola }),
            MsOptions.Create(new MongoOptions { ConnectionString = mongo, Database = baseDeDatos }),
            new ServiceCollection().BuildServiceProvider(),
            NullLogger<NotificationEventsConsumer>.Instance);

        await consumidor.StartAsync(CancellationToken.None);

        await consumidor.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(consumidor.ExecuteTask.IsCompletedSuccessfully);
    }
}
