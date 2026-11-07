using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Notificaciones.Tests;

public class EndpointsAutenticacionTests : IClassFixture<NotificacionesApiFactory>
{
    private readonly NotificacionesApiFactory _factory;

    public EndpointsAutenticacionTests(NotificacionesApiFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("PUT", "/notifications/directory/me")]
    [InlineData("POST", "/notifications/devices")]
    [InlineData("DELETE", "/notifications/devices/token-1")]
    [InlineData("GET", "/notifications")]
    [InlineData("GET", "/notifications/unread-count")]
    [InlineData("PATCH", "/notifications/6711a2f0c3e4b5a6d7e8f901/read")]
    [InlineData("POST", "/notifications/read-all")]
    [InlineData("GET", "/notifications/templates")]
    [InlineData("GET", "/notifications/templates/cv.generated/es")]
    [InlineData("PUT", "/notifications/templates/cv.generated/es")]
    public async Task SinXUserId_Responde401ConFormatoDeError(string metodo, string ruta)
    {
        // Con un cuerpo válido, para que el 401 no dependa de la validación.
        var solicitud = new HttpRequestMessage(new HttpMethod(metodo), ruta);
        if (metodo is "PUT" && ruta.Contains("templates")) solicitud.Content = JsonContent.Create(new { title = "T", body = "B" });
        else if (metodo is "PUT") solicitud.Content = JsonContent.Create(new { language = "es" });
        if (metodo is "POST" && ruta.EndsWith("devices")) solicitud.Content = JsonContent.Create(new { token = "t", platform = "android" });

        // Con rol admin: aun así, sin X-User-Id no hay identidad.
        var response = await _factory.CrearCliente(userId: null, role: "admin").SendAsync(solicitud);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(401, body.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Falta el encabezado X-User-Id.", body.RootElement.GetProperty("message").GetString());
    }
}
