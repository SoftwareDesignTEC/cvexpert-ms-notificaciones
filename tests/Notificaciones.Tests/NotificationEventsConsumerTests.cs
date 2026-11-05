using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Notificaciones.Api.Infrastructure.Mongo;
using Notificaciones.Api.Infrastructure.ServiceBus;
using Notificaciones.Api.Options;
using Notificaciones.Api.Services;
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
        var proveedor = new ServiceCollection().BuildServiceProvider();
        var mongoOptions = MsOptions.Create(new MongoOptions { ConnectionString = mongo, Database = baseDeDatos });
        var consumidor = new NotificationEventsConsumer(
            MsOptions.Create(new ServiceBusOptions { ConnectionString = serviceBus, QueueName = cola }),
            mongoOptions,
            new MongoIndexInitializer(mongoOptions, proveedor, NullLogger<MongoIndexInitializer>.Instance),
            proveedor,
            NullLogger<NotificationEventsConsumer>.Instance);

        await consumidor.StartAsync(CancellationToken.None);

        await consumidor.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(consumidor.ExecuteTask.IsCompletedSuccessfully);
    }
}

public class MongoIndexInitializerTests
{
    [Fact]
    public async Task SinMongo_TerminaSinCrearIndices()
    {
        var inicializador = new MongoIndexInitializer(
            MsOptions.Create(new MongoOptions()),
            new ServiceCollection().BuildServiceProvider(),
            NullLogger<MongoIndexInitializer>.Instance);

        await inicializador.StartAsync(CancellationToken.None);

        await inicializador.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(inicializador.ExecuteTask.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task ConMongo_CreaLosIndicesYAvisaQueEstaListo()
    {
        var notificaciones = new RepositorioConIndices();
        var dispositivos = new DispositivosConIndices();
        var proveedor = new ServiceCollection()
            .AddSingleton<INotificationRepository>(notificaciones)
            .AddSingleton<IDeviceRepository>(dispositivos)
            .BuildServiceProvider();
        var inicializador = new MongoIndexInitializer(
            MsOptions.Create(new MongoOptions { ConnectionString = "mongodb://localhost", Database = "notificaciones" }),
            proveedor,
            NullLogger<MongoIndexInitializer>.Instance);

        await inicializador.StartAsync(CancellationToken.None);

        await inicializador.WaitUntilReadyAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(notificaciones.IndicesCreados);
        Assert.True(dispositivos.IndicesCreados);
    }

    private sealed class RepositorioConIndices : InMemoryNotificationRepository, INotificationRepository
    {
        public bool IndicesCreados { get; private set; }

        Task INotificationRepository.EnsureIndexesAsync(CancellationToken cancellationToken)
        {
            IndicesCreados = true;
            return Task.CompletedTask;
        }
    }

    private sealed class DispositivosConIndices : InMemoryDeviceRepository, IDeviceRepository
    {
        public bool IndicesCreados { get; private set; }

        Task IDeviceRepository.EnsureIndexesAsync(CancellationToken cancellationToken)
        {
            IndicesCreados = true;
            return Task.CompletedTask;
        }
    }
}
