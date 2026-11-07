using System.Net;
using System.Text.Json;

namespace Notificaciones.Tests;

public class SwaggerTests : IClassFixture<NotificacionesApiFactory>
{
    private readonly NotificacionesApiFactory _factory;

    public SwaggerTests(NotificacionesApiFactory factory)
    {
        _factory = factory;
    }

    // Swashbuckle falla en tiempo de ejecución (rutas ambiguas, esquemas con el mismo nombre): se genera el documento.
    [Fact]
    public async Task DocumentoSwagger_SeGeneraConLasRutasDelContrato()
    {
        var response = await _factory.CreateClient().GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var rutas = document.RootElement.GetProperty("paths").EnumerateObject().Select(p => p.Name).ToHashSet();
        Assert.Superset(
            new HashSet<string>
            {
                "/notifications/directory/me",
                "/notifications/devices",
                "/notifications/devices/{token}",
                "/notifications",
                "/notifications/unread-count",
                "/notifications/{id}/read",
                "/notifications/read-all",
                "/notifications/templates",
                "/notifications/templates/{type}/{language}",
            },
            rutas);
    }
}
