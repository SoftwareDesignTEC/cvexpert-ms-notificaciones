using System.Net;
using System.Text.Json;

namespace Notificaciones.Tests;

public class HealthControllerTests : IClassFixture<NotificacionesApiFactory>
{
    private readonly HttpClient _client;

    public HealthControllerTests(NotificacionesApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetHealth_RespondeOkConStatus()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("ok", body.RootElement.GetProperty("status").GetString());
    }
}
