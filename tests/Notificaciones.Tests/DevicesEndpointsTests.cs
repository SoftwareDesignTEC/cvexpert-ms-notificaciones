using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Notificaciones.Tests;

public class DevicesEndpointsTests : IClassFixture<NotificacionesApiFactory>
{
    private const string Ruta = "/notifications/devices";
    private readonly NotificacionesApiFactory _factory;
    private readonly string _ana = $"ana-{Guid.NewGuid():N}";
    private readonly string _beto = $"beto-{Guid.NewGuid():N}";

    // Los tokens de FCM incluyen ":".
    private readonly string _token = $"fGx1Yz8sQ0a3:APA91bH-{Guid.NewGuid():N}";

    public DevicesEndpointsTests(NotificacionesApiFactory factory)
    {
        _factory = factory;
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private Task<HttpResponseMessage> Registrar(string usuario, object cuerpo) =>
        _factory.CrearCliente(usuario).PostAsJsonAsync(Ruta, cuerpo);

    private Task<HttpResponseMessage> Borrar(string usuario, string token) =>
        _factory.CrearCliente(usuario).DeleteAsync($"{Ruta}/{Uri.EscapeDataString(token)}");

    [Fact]
    public async Task TokenNuevo_Responde201ConElDispositivo()
    {
        var response = await Registrar(_ana, new { token = _token, platform = "android" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await Json(response);
        Assert.Equal(_token, body.GetProperty("token").GetString());
        Assert.Equal("android", body.GetProperty("platform").GetString());
        Assert.True(body.GetProperty("registeredAt").GetInt64() > 0);
        Assert.Equal(_ana, _factory.Dispositivos.Find(_token)!.UserId);
    }

    [Fact]
    public async Task MismoTokenMismoUsuario_Responde200YConservaRegisteredAt()
    {
        var primero = await Json(await Registrar(_ana, new { token = _token, platform = "android" }));

        var response = await Registrar(_ana, new { token = _token, platform = "android" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(primero.GetProperty("registeredAt").GetInt64(), (await Json(response)).GetProperty("registeredAt").GetInt64());
    }

    [Fact]
    public async Task TokenDeOtroUsuario_Responde200YPasaAlUsuarioActual()
    {
        await Registrar(_ana, new { token = _token, platform = "android" });

        var response = await Registrar(_beto, new { token = _token, platform = "android" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(_beto, _factory.Dispositivos.Find(_token)!.UserId);
    }

    [Theory]
    [InlineData("web")]
    [InlineData("ios")]
    [InlineData("")]
    public async Task PlataformaInvalida_Responde400(string plataforma)
    {
        var response = await Registrar(_ana, new { token = _token, platform = plataforma });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Debe ser \"android\".", (await Json(response)).GetProperty("errors").GetProperty("platform").GetString());
        Assert.Null(_factory.Dispositivos.Find(_token));
    }

    [Fact]
    public async Task SinToken_Responde400()
    {
        var response = await Registrar(_ana, new { platform = "android" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await Json(response)).GetProperty("errors").TryGetProperty("token", out _));
    }

    [Fact]
    public async Task BorrarTokenPropio_Responde204YLoQuita()
    {
        await Registrar(_ana, new { token = _token, platform = "android" });

        var response = await Borrar(_ana, _token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null(_factory.Dispositivos.Find(_token));
    }

    [Fact]
    public async Task BorrarTokenAjeno_Responde204SinTocarlo()
    {
        await Registrar(_ana, new { token = _token, platform = "android" });

        var response = await Borrar(_beto, _token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(_ana, _factory.Dispositivos.Find(_token)!.UserId);
    }

    [Fact]
    public async Task BorrarTokenInexistente_Responde204()
    {
        var response = await Borrar(_ana, "no-existe");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
