using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Bson;
using Notificaciones.Api.Contracts;
using Notificaciones.Api.Domain;
using Notificaciones.Api.Hubs;
using Notificaciones.Api.Services;

namespace Notificaciones.Tests;

public class SignalRNotificationDispatcherTests
{
    private readonly HubFalso _hub = new();
    private readonly InMemoryDirectoryRepository _directorio = new();
    private readonly SignalRNotificationDispatcher _dispatcher;

    public SignalRNotificationDispatcherTests()
    {
        _dispatcher = new SignalRNotificationDispatcher(_hub, _directorio, new DefaultNotificationRenderer());
    }

    private static Notification Notificacion() => new()
    {
        Id = "6711a2f0c3e4b5a6d7e8f901",
        EventId = "3f2c6a1e-8b4d-4c7a-9e21-5d6f7a8b9c0d",
        UserId = "usuario-1",
        Type = "application.status_changed",
        Source = "ofertas-postulaciones",
        Data = new BsonDocument
        {
            ["applicationId"] = "1532",
            ["jobOfferId"] = "88",
            ["jobTitle"] = "Desarrollador .NET",
            ["company"] = "Acme",
            ["newStatus"] = "interview",
        },
        OccurredAt = 1760716800000,
        CreatedAt = 1760720400000,
        ReadAt = null,
    };

    [Fact]
    public async Task EnviaANotificationDelUsuarioConLaFormaDelContrato()
    {
        await _dispatcher.DispatchAsync(Notificacion(), CancellationToken.None);

        var envio = Assert.Single(_hub.Envios);
        Assert.Equal("usuario-1", envio.Usuario);
        Assert.Equal("notification", envio.Metodo);

        var payload = Assert.IsType<NotificationResponse>(Assert.Single(envio.Argumentos));
        Assert.Equal("6711a2f0c3e4b5a6d7e8f901", payload.Id);
        Assert.Equal("application.status_changed", payload.Type);
        Assert.Equal("Tu postulación cambió de estado", payload.Title);
        Assert.Equal("Tu postulación a Desarrollador .NET en Acme cambió a \"Entrevista\".", payload.Body);
        Assert.Equal("interview", payload.Data["newStatus"]!.GetValue<string>());
        Assert.Equal("1532", payload.Data["applicationId"]!.GetValue<string>());
        Assert.False(payload.Read);
        Assert.Equal(1760720400000, payload.CreatedAt);
        Assert.Null(payload.ReadAt);
    }

    [Fact]
    public async Task UsuarioConInglesEnElDirectorio_RecibeLosTextosEnIngles()
    {
        await _directorio.UpsertAsync(
            new DirectoryEntry { UserId = "usuario-1", Email = "ana@ejemplo.com", Language = "en" }, CancellationToken.None);

        await _dispatcher.DispatchAsync(Notificacion(), CancellationToken.None);

        var payload = Assert.IsType<NotificationResponse>(Assert.Single(Assert.Single(_hub.Envios).Argumentos));
        Assert.Equal("Your application was updated", payload.Title);
        Assert.Equal("Your application for Desarrollador .NET at Acme changed to \"Interview\".", payload.Body);
    }

    [Fact]
    public void PayloadSerializado_TieneSoloLosCamposDelContrato()
    {
        var payload = NotificationResponse.From(Notificacion(), new RenderedText("t", "b"));

        // Mismas opciones que el protocolo JSON de SignalR (camelCase). readAt se omite mientras no está leída.
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var campos = json.RootElement.EnumerateObject().Select(p => p.Name).ToArray();

        Assert.Equal(["id", "type", "title", "body", "data", "read", "createdAt"], campos);
    }

    [Fact]
    public void NotificacionLeida_IncluyeReadAt()
    {
        var notificacion = Notificacion();
        notificacion.ReadAt = 1760724000000;

        var payload = NotificationResponse.From(notificacion, new RenderedText("t", "b"));

        Assert.True(payload.Read);
        Assert.Equal(1760724000000, payload.ReadAt);
    }

    private sealed record Envio(string Usuario, string Metodo, object?[] Argumentos);

    private sealed class HubFalso : IHubContext<NotificationsHub>
    {
        private readonly ClientesFalsos _clientes = new();

        public List<Envio> Envios => _clientes.Envios;
        public IHubClients Clients => _clientes;
        public IGroupManager Groups => throw new NotSupportedException();
    }

    // Solo implementa Clients.User(...): cualquier otro destino hace fallar la prueba.
    private sealed class ClientesFalsos : IHubClients
    {
        public List<Envio> Envios { get; } = [];

        public IClientProxy User(string userId) => new ProxyFalso(userId, Envios);

        public IClientProxy All => throw new NotSupportedException();
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
        public IClientProxy Client(string connectionId) => throw new NotSupportedException();
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw new NotSupportedException();
        public IClientProxy Group(string groupName) => throw new NotSupportedException();
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => throw new NotSupportedException();
        public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotSupportedException();
    }

    private sealed class ProxyFalso(string usuario, List<Envio> envios) : IClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            envios.Add(new Envio(usuario, method, args));
            return Task.CompletedTask;
        }
    }
}
