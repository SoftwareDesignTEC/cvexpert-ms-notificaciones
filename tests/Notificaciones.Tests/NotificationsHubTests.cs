using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Notificaciones.Api.Hubs;

namespace Notificaciones.Tests;

public class NotificationsHubTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly TimeSpan Espera = TimeSpan.FromSeconds(10);
    private readonly WebApplicationFactory<Program> _factory;

    public NotificationsHubTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UsuarioConEncabezado_RecibeMensajesEnviadosASuId()
    {
        await using var conexion = CrearConexion(userId: "usuario-1");
        var recibido = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        conexion.On<string>("Prueba", mensaje => recibido.TrySetResult(mensaje));
        await conexion.StartAsync();

        var hub = _factory.Services.GetRequiredService<IHubContext<NotificationsHub>>();
        // Se reintenta porque el servidor registra al usuario justo después del handshake.
        using var limite = new CancellationTokenSource(Espera);
        while (!recibido.Task.IsCompleted)
        {
            await hub.Clients.User("usuario-1").SendAsync("Prueba", "hola", limite.Token);
            await Task.WhenAny(recibido.Task, Task.Delay(100, limite.Token));
        }

        Assert.Equal("hola", await recibido.Task);
    }

    [Fact]
    public async Task ConexionSinEncabezado_EsCerradaPorElServidor()
    {
        await using var conexion = CrearConexion(userId: null);
        var cerrada = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        conexion.Closed += _ => { cerrada.TrySetResult(); return Task.CompletedTask; };

        await conexion.StartAsync();

        await cerrada.Task.WaitAsync(Espera);
        Assert.Equal(HubConnectionState.Disconnected, conexion.State);
    }

    // TestServer no expone WebSockets con encabezados propios, así que se usa long polling.
    private HubConnection CrearConexion(string? userId) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, "hubs/notifications"), options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                if (userId is not null) options.Headers[HeaderUserIdProvider.UserIdHeader] = userId;
            })
            .Build();
}
