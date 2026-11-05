using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Notificaciones.Tests;

public class DirectoryEndpointsTests : IClassFixture<NotificacionesApiFactory>
{
    private const string Ruta = "/notifications/directory/me";
    private readonly NotificacionesApiFactory _factory;
    private readonly string _usuario = $"usuario-{Guid.NewGuid():N}";

    public DirectoryEndpointsTests(NotificacionesApiFactory factory)
    {
        _factory = factory;
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    [Fact]
    public async Task PutMe_GuardaYDevuelveLaEntrada()
    {
        var response = await _factory.CrearCliente(_usuario, "ana@ejemplo.com").PutAsJsonAsync(Ruta, new { language = "en" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await Json(response);
        Assert.Equal(_usuario, body.GetProperty("userId").GetString());
        Assert.Equal("ana@ejemplo.com", body.GetProperty("email").GetString());
        Assert.Equal("en", body.GetProperty("language").GetString());
        Assert.False(body.TryGetProperty("updatedAt", out _));

        var guardada = await _factory.Directorio.GetAsync(_usuario, CancellationToken.None);
        Assert.NotNull(guardada);
        Assert.Equal("en", guardada.Language);
        Assert.True(guardada.UpdatedAt > 0);
    }

    [Fact]
    public async Task PutMeOtraVez_ActualizaIdiomaYCorreo()
    {
        await _factory.CrearCliente(_usuario, "ana@ejemplo.com").PutAsJsonAsync(Ruta, new { language = "es" });

        var response = await _factory.CrearCliente(_usuario, "ana.nueva@ejemplo.com").PutAsJsonAsync(Ruta, new { language = "en" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var guardada = await _factory.Directorio.GetAsync(_usuario, CancellationToken.None);
        Assert.Equal("ana.nueva@ejemplo.com", guardada!.Email);
        Assert.Equal("en", guardada.Language);
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("ES")]
    [InlineData("")]
    public async Task IdiomaInvalido_Responde400(string idioma)
    {
        var response = await _factory.CrearCliente(_usuario, "ana@ejemplo.com").PutAsJsonAsync(Ruta, new { language = idioma });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await Json(response);
        Assert.Equal("Debe ser \"es\" o \"en\".", body.GetProperty("errors").GetProperty("language").GetString());
        Assert.Null(await _factory.Directorio.GetAsync(_usuario, CancellationToken.None));
    }

    [Fact]
    public async Task SinIdioma_Responde400()
    {
        var response = await _factory.CrearCliente(_usuario, "ana@ejemplo.com").PutAsJsonAsync(Ruta, new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await Json(response)).GetProperty("errors").TryGetProperty("language", out _));
    }

    [Fact]
    public async Task SinXUserEmail_Responde401()
    {
        var response = await _factory.CrearCliente(_usuario).PutAsJsonAsync(Ruta, new { language = "es" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Falta el encabezado X-User-Email.", (await Json(response)).GetProperty("message").GetString());
        Assert.Null(await _factory.Directorio.GetAsync(_usuario, CancellationToken.None));
    }
}
