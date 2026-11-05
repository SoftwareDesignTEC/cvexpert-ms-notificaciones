using Microsoft.Extensions.Logging.Abstractions;
using Notificaciones.Api.Domain;
using Notificaciones.Api.Services;
using Notificaciones.Api.Services.Events;

namespace Notificaciones.Tests;

public class NotificationEventHandlerTests
{
    private static readonly DateTimeOffset Ahora = DateTimeOffset.FromUnixTimeMilliseconds(1760720400000);

    private readonly RepositorioFalso _repositorio = new();
    private readonly DespachadorFalso _despachador = new();
    private readonly NotificationEventHandler _handler;

    public NotificationEventHandlerTests()
    {
        _handler = new NotificationEventHandler(
            _repositorio, _despachador, new RelojFijo(Ahora), NullLogger<NotificationEventHandler>.Instance)
        {
            AbandonDelay = TimeSpan.Zero,
        };
    }

    private static MensajeFalso MensajeValido() =>
        new(NotificationEventValidatorTests.EventoValido("application.status_changed").ToJsonString());

    [Fact]
    public async Task EventoValido_SeGuardaYSeCompleta()
    {
        var mensaje = MensajeValido();

        await _handler.HandleAsync(mensaje, CancellationToken.None);

        var guardada = Assert.Single(_repositorio.Guardadas);
        Assert.Equal(NotificationEventValidatorTests.EventId, guardada.EventId);
        Assert.Equal("usuario-1", guardada.UserId);
        Assert.Equal("application.status_changed", guardada.Type);
        Assert.Equal("ofertas-postulaciones", guardada.Source);
        Assert.Equal(1760716800000, guardada.OccurredAt);
        Assert.Equal(Ahora.ToUnixTimeMilliseconds(), guardada.CreatedAt);
        Assert.Null(guardada.ReadAt);
        Assert.Equal("interview", guardada.Data["newStatus"].AsString);

        Assert.Same(guardada, Assert.Single(_despachador.Despachadas));
        Assert.Equal(Resultado.Completado, mensaje.Resultado);
    }

    [Fact]
    public async Task EventoInvalido_VaADeadLetterSinGuardar()
    {
        var evento = NotificationEventValidatorTests.EventoValido("job.new");
        evento["type"] = "user.deleted";
        var mensaje = new MensajeFalso(evento.ToJsonString());

        await _handler.HandleAsync(mensaje, CancellationToken.None);

        Assert.Equal(Resultado.DeadLetter, mensaje.Resultado);
        Assert.Equal(DeadLetterReasons.SchemaValidationFailed, mensaje.DeadLetterReason);
        Assert.Contains("type: \"user.deleted\" no es un valor permitido", mensaje.DeadLetterDescription);
        Assert.Empty(_repositorio.Guardadas);
        Assert.Empty(_despachador.Despachadas);
    }

    [Fact]
    public async Task JsonInvalido_VaADeadLetterComoInvalidJson()
    {
        var mensaje = new MensajeFalso("{ roto");

        await _handler.HandleAsync(mensaje, CancellationToken.None);

        Assert.Equal(Resultado.DeadLetter, mensaje.Resultado);
        Assert.Equal(DeadLetterReasons.InvalidJson, mensaje.DeadLetterReason);
        Assert.Empty(_repositorio.Guardadas);
    }

    [Fact]
    public async Task EventoDuplicado_SeCompletaSinVolverAEntregar()
    {
        _repositorio.Resultado = InsertResult.Duplicate;
        var mensaje = MensajeValido();

        await _handler.HandleAsync(mensaje, CancellationToken.None);

        Assert.Equal(Resultado.Completado, mensaje.Resultado);
        Assert.Empty(_despachador.Despachadas);
    }

    [Fact]
    public async Task ErrorDeMongo_SeAbandonaParaReintentar()
    {
        _repositorio.Error = new TimeoutException("Mongo no responde");
        var mensaje = MensajeValido();

        await _handler.HandleAsync(mensaje, CancellationToken.None);

        Assert.Equal(Resultado.Abandonado, mensaje.Resultado);
        Assert.Empty(_despachador.Despachadas);
    }

    [Fact]
    public async Task FalloDelDespachador_SeCompletaIgual()
    {
        _despachador.Error = new InvalidOperationException("FCM caído");
        var mensaje = MensajeValido();

        await _handler.HandleAsync(mensaje, CancellationToken.None);

        Assert.Single(_repositorio.Guardadas);
        Assert.Equal(Resultado.Completado, mensaje.Resultado);
    }

    private enum Resultado { Pendiente, Completado, DeadLetter, Abandonado }

    private sealed class MensajeFalso(string cuerpo) : IQueueMessage
    {
        public Resultado Resultado { get; private set; } = Resultado.Pendiente;
        public string? DeadLetterReason { get; private set; }
        public string? DeadLetterDescription { get; private set; }

        public string MessageId => NotificationEventValidatorTests.EventId;
        public string? Subject => "application.status_changed";
        public int DeliveryCount => 1;
        public BinaryData Body { get; } = BinaryData.FromString(cuerpo);

        public Task CompleteAsync(CancellationToken cancellationToken) => Cerrar(Resultado.Completado);

        public Task DeadLetterAsync(string reason, string description, CancellationToken cancellationToken)
        {
            DeadLetterReason = reason;
            DeadLetterDescription = description;
            return Cerrar(Resultado.DeadLetter);
        }

        public Task AbandonAsync(CancellationToken cancellationToken) => Cerrar(Resultado.Abandonado);

        // Un mensaje se cierra una sola vez.
        private Task Cerrar(Resultado resultado)
        {
            Assert.Equal(Resultado.Pendiente, Resultado);
            Resultado = resultado;
            return Task.CompletedTask;
        }
    }

    private sealed class RepositorioFalso : INotificationRepository
    {
        public List<Notification> Guardadas { get; } = [];
        public InsertResult Resultado { get; set; } = InsertResult.Inserted;
        public Exception? Error { get; set; }

        public Task EnsureIndexesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<InsertResult> InsertAsync(Notification notification, CancellationToken cancellationToken)
        {
            if (Error is not null) throw Error;
            if (Resultado == InsertResult.Inserted) Guardadas.Add(notification);
            return Task.FromResult(Resultado);
        }

        // El handler solo guarda.
        public Task<Page<Notification>> ListAsync(string userId, bool? unread, int page, int pageSize, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<long> CountUnreadAsync(string userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Notification?> MarkReadAsync(string id, string userId, long readAt, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task MarkAllReadAsync(string userId, long readAt, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class DespachadorFalso : INotificationDispatcher
    {
        public List<Notification> Despachadas { get; } = [];
        public Exception? Error { get; set; }

        public Task DispatchAsync(Notification notification, CancellationToken cancellationToken)
        {
            if (Error is not null) throw Error;
            Despachadas.Add(notification);
            return Task.CompletedTask;
        }
    }

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }
}
