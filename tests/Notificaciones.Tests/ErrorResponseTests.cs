using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Notificaciones.Tests;

public class ErrorResponseTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ErrorResponseTests(WebApplicationFactory<Program> factory)
    {
        // Agrega ErroresPruebaController (definido abajo, solo existe en las pruebas) a la API.
        _client = factory
            .WithWebHostBuilder(b => b.ConfigureServices(services =>
                services.AddControllers().AddApplicationPart(typeof(ErroresPruebaController).Assembly)))
            .CreateClient();
    }

    [Fact]
    public async Task ModeloInvalido_Responde400ConErroresPorCampo()
    {
        var response = await _client.PostAsJsonAsync("/pruebas/errores/validar", new { correo = "no-es-correo" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await LeerJson(response);
        Assert.Equal(400, body.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("message").GetString()));

        var errors = body.GetProperty("errors");
        Assert.True(errors.TryGetProperty("nombre", out _));
        Assert.True(errors.TryGetProperty("correo", out _));
        Assert.False(body.TryGetProperty("title", out _)); // no es ProblemDetails
    }

    [Fact]
    public async Task ExcepcionNoControlada_Responde500SinErrors()
    {
        var response = await _client.GetAsync("/pruebas/errores/excepcion");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await LeerJson(response);
        Assert.Equal(500, body.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("message").GetString()));
        Assert.False(body.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task RutaInexistente_Responde404ConFormatoDeError()
    {
        var response = await _client.GetAsync("/no-existe");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await LeerJson(response);
        Assert.Equal(404, body.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("message").GetString()));
        Assert.False(body.TryGetProperty("errors", out _));
    }

    private static async Task<JsonElement> LeerJson(HttpResponseMessage response)
    {
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }
}

public class SolicitudPrueba
{
    [Required] public string? Nombre { get; set; }
    [EmailAddress] public string? Correo { get; set; }
}

[ApiController]
[Route("pruebas/errores")]
public class ErroresPruebaController : ControllerBase
{
    [HttpPost("validar")]
    public IActionResult Validar(SolicitudPrueba solicitud) => Ok();

    [HttpGet("excepcion")]
    public IActionResult Excepcion() => throw new InvalidOperationException("Falla de prueba");
}
