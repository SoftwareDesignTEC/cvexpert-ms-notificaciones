using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Notificaciones.Api.Auth;
using Notificaciones.Api.Hubs;

namespace Notificaciones.Tests;

// Con SignalR local (sin Azure:SignalR:ConnectionString). El negotiate de Azure SignalR Service, que devuelve url y
// accessToken, solo se puede probar contra un servicio real.
public class NotificationsHubTests : IClassFixture<NotificacionesApiFactory>
{
    private const string Negotiate = "/notifications/hub/negotiate?negotiateVersion=1";
    private static readonly TimeSpan Espera = TimeSpan.FromSeconds(10);
    private readonly NotificacionesApiFactory _factory;

    public NotificationsHubTests(NotificacionesApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task NegotiateSinUsuario_Responde401ConFormatoDeError()
    {
        var response = await _factory.CreateClient().PostAsync(Negotiate, null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(401, body.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task NegotiateConUsuario_Responde200()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(ApimHeadersAuthenticationHandler.UserIdHeader, "usuario-1");

        var response = await client.PostAsync(Negotiate, null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RutaAnteriorDelHub_YaNoExiste()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(ApimHeadersAuthenticationHandler.UserIdHeader, "usuario-1");

        var response = await client.PostAsync("/hubs/notifications/negotiate?negotiateVersion=1", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
    public async Task ConexionSinEncabezado_EsRechazada()
    {
        await using var conexion = CrearConexion(userId: null);

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => conexion.StartAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);
    }

    // TestServer no expone WebSockets con encabezados propios, así que se usa long polling.
    private HubConnection CrearConexion(string? userId) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, "notifications/hub"), options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                if (userId is not null) options.Headers[ApimHeadersAuthenticationHandler.UserIdHeader] = userId;
            })
            .Build();
}
