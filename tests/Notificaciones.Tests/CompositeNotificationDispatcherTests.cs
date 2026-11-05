using Microsoft.Extensions.Logging.Abstractions;
using Notificaciones.Api.Domain;
using Notificaciones.Api.Services;
using Notificaciones.Api.Services.Channels;

namespace Notificaciones.Tests;

public class CompositeNotificationDispatcherTests
{
    private readonly InMemoryDirectoryRepository _directorio = new();

    private CompositeNotificationDispatcher Compuesto(IDirectoryRepository? directorio, params INotificationChannel[] canales) =>
        new(canales, directorio ?? _directorio, new DefaultNotificationRenderer(), NullLogger<CompositeNotificationDispatcher>.Instance);

    [Fact]
    public async Task LlamaATodosLosCanales()
    {
        var signalr = new CanalFalso("signalr");
        var fcm = new CanalFalso("fcm");
        var correo = new CanalFalso("email");

        await Compuesto(null, signalr, fcm, correo).DispatchAsync(EntregasDePrueba.Notificacion(), CancellationToken.None);

        Assert.Single(signalr.Entregas);
        Assert.Single(fcm.Entregas);
        Assert.Single(correo.Entregas);
    }

    [Fact]
    public async Task FalloDeUnCanal_NoAfectaALosDemas()
    {
        var signalr = new CanalFalso("signalr");
        var fcm = new CanalFalso("fcm") { Error = new InvalidOperationException("FCM caído") };
        var correo = new CanalFalso("email");

        await Compuesto(null, signalr, fcm, correo).DispatchAsync(EntregasDePrueba.Notificacion(), CancellationToken.None);

        Assert.Single(signalr.Entregas);
        Assert.Single(correo.Entregas);
    }

    [Fact]
    public async Task LosCanalesCorrenEnParalelo()
    {
        // El primer canal solo termina cuando el segundo ya empezó: en serie, esto no terminaría nunca.
        var segundoEmpezo = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var primero = new CanalFalso("signalr") { Espera = segundoEmpezo.Task };
        var segundo = new CanalFalso("fcm") { AlEmpezar = () => segundoEmpezo.TrySetResult() };

        await Compuesto(null, primero, segundo)
            .DispatchAsync(EntregasDePrueba.Notificacion(), CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Single(primero.Entregas);
        Assert.Single(segundo.Entregas);
    }

    [Fact]
    public async Task UsuarioEnElDirectorio_EntregaConSuIdiomaYSusDatos()
    {
        await _directorio.UpsertAsync(EntregasDePrueba.Ana("en"), CancellationToken.None);
        var canal = new CanalFalso("email");

        await Compuesto(null, canal).DispatchAsync(EntregasDePrueba.Notificacion(), CancellationToken.None);

        var entrega = Assert.Single(canal.Entregas);
        Assert.Equal("en", entrega.Language);
        Assert.Equal("ana@ejemplo.com", entrega.Recipient!.Email);
        Assert.Equal("Your application was updated", entrega.Text.Title);
    }

    [Fact]
    public async Task UsuarioSinDirectorio_EntregaEnEspanolSinDestinatario()
    {
        var canal = new CanalFalso("signalr");

        await Compuesto(null, canal).DispatchAsync(EntregasDePrueba.Notificacion(), CancellationToken.None);

        var entrega = Assert.Single(canal.Entregas);
        Assert.Equal("es", entrega.Language);
        Assert.Null(entrega.Recipient);
        Assert.Equal("Tu postulación cambió de estado", entrega.Text.Title);
    }

    [Fact]
    public async Task DirectorioCaido_EntregaIgualEnEspanolSinDestinatario()
    {
        var canal = new CanalFalso("signalr");

        await Compuesto(new DirectorioCaido(), canal).DispatchAsync(EntregasDePrueba.Notificacion(), CancellationToken.None);

        var entrega = Assert.Single(canal.Entregas);
        Assert.Equal("es", entrega.Language);
        Assert.Null(entrega.Recipient);
    }

    private sealed class CanalFalso(string nombre) : INotificationChannel
    {
        public List<NotificationDelivery> Entregas { get; } = [];
        public Exception? Error { get; init; }
        public Task Espera { get; init; } = Task.CompletedTask;
        public Action? AlEmpezar { get; init; }

        public string Name => nombre;

        public async Task SendAsync(NotificationDelivery delivery, CancellationToken cancellationToken)
        {
            await Task.Yield();
            AlEmpezar?.Invoke();
            await Espera;
            if (Error is not null) throw Error;
            Entregas.Add(delivery);
        }
    }

    private sealed class DirectorioCaido : IDirectoryRepository
    {
        public Task UpsertAsync(DirectoryEntry entry, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<DirectoryEntry?> GetAsync(string userId, CancellationToken cancellationToken) => throw new TimeoutException("Mongo no responde");
    }
}
