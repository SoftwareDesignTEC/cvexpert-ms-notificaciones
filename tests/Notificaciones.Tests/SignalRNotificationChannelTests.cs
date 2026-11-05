using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Notificaciones.Api.Contracts;
using Notificaciones.Api.Hubs;
using Notificaciones.Api.Services;

namespace Notificaciones.Tests;

public class SignalRNotificationChannelTests
{
    private readonly HubFalso _hub = new();
    private readonly SignalRNotificationChannel _canal;

    public SignalRNotificationChannelTests()
    {
        _canal = new SignalRNotificationChannel(_hub);
    }

    [Fact]
    public async Task EnviaANotificationDelUsuarioConLaFormaDelContrato()
    {
        await _canal.SendAsync(EntregasDePrueba.Entrega(), CancellationToken.None);

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
    public async Task UsaLosTextosDeLaEntrega()
    {
        await _canal.SendAsync(EntregasDePrueba.Entrega(destinatario: EntregasDePrueba.Ana("en")), CancellationToken.None);

        var payload = Assert.IsType<NotificationResponse>(Assert.Single(Assert.Single(_hub.Envios).Argumentos));
        Assert.Equal("Your application was updated", payload.Title);
    }

    [Fact]
    public void PayloadSerializado_TieneSoloLosCamposDelContrato()
    {
        var payload = NotificationResponse.From(EntregasDePrueba.Notificacion(), new RenderedText("t", "b"));

        // Mismas opciones que el protocolo JSON de SignalR (camelCase). readAt se omite mientras no está leída.
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var campos = json.RootElement.EnumerateObject().Select(p => p.Name).ToArray();

        Assert.Equal(["id", "type", "title", "body", "data", "read", "createdAt"], campos);
    }

    [Fact]
    public void NotificacionLeida_IncluyeReadAt()
    {
        var notificacion = EntregasDePrueba.Notificacion();
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
