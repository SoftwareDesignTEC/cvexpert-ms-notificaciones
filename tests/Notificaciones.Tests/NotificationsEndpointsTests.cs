using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MongoDB.Bson;
using Notificaciones.Api.Domain;

namespace Notificaciones.Tests;

public class NotificationsEndpointsTests : IClassFixture<NotificacionesApiFactory>
{
    private const long Base = 1760720400000;
    private readonly NotificacionesApiFactory _factory;
    private readonly string _ana = $"ana-{Guid.NewGuid():N}";
    private readonly string _beto = $"beto-{Guid.NewGuid():N}";

    public NotificationsEndpointsTests(NotificacionesApiFactory factory)
    {
        _factory = factory;
    }

    /// <summary>Guarda notificaciones cv.generated con createdAt = Base + i; las de <paramref name="leidas"/> ya están leídas.</summary>
    private async Task<List<Notification>> Sembrar(string usuario, int cantidad, params int[] leidas)
    {
        var creadas = new List<Notification>();
        for (var i = 0; i < cantidad; i++)
        {
            var notificacion = new Notification
            {
                EventId = Guid.NewGuid().ToString(),
                UserId = usuario,
                Type = "cv.generated",
                Source = "cvs",
                Data = new BsonDocument { ["cvId"] = $"cv-{i}", ["title"] = $"CV {i}" },
                OccurredAt = Base + i,
                CreatedAt = Base + i,
                ReadAt = leidas.Contains(i) ? Base + 1000 + i : null,
            };
            await _factory.Notificaciones.InsertAsync(notificacion, CancellationToken.None);
            creadas.Add(notificacion);
        }
        return creadas;
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private async Task<JsonElement> Listar(string usuario, string consulta = "")
    {
        var response = await _factory.CrearCliente(usuario).GetAsync($"/notifications{consulta}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await Json(response);
    }

    private static long[] CreatedAts(JsonElement pagina) =>
        pagina.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("createdAt").GetInt64()).ToArray();

    [Fact]
    public async Task Listar_PorDefectoPrimeraPaginaDeDiezMasRecientesPrimero()
    {
        await Sembrar(_ana, 12);

        var pagina = await Listar(_ana);

        Assert.Equal(12, pagina.GetProperty("totalRecords").GetInt64());
        Assert.Equal(1, pagina.GetProperty("page").GetInt32());
        Assert.Equal(10, pagina.GetProperty("pageSize").GetInt32());
        Assert.Equal(Enumerable.Range(2, 10).Reverse().Select(i => Base + i), CreatedAts(pagina));
    }

    [Fact]
    public async Task Listar_SegundaYUltimaPagina()
    {
        await Sembrar(_ana, 25);

        var segunda = await Listar(_ana, "?page=2&pageSize=10");
        var tercera = await Listar(_ana, "?page=3&pageSize=10");
        var cuarta = await Listar(_ana, "?page=4&pageSize=10");

        Assert.Equal(Enumerable.Range(5, 10).Reverse().Select(i => Base + i), CreatedAts(segunda));
        Assert.Equal(Enumerable.Range(0, 5).Reverse().Select(i => Base + i), CreatedAts(tercera));
        Assert.Empty(CreatedAts(cuarta));
        Assert.Equal(25, cuarta.GetProperty("totalRecords").GetInt64());
    }

    [Fact]
    public async Task Listar_FiltroUnread()
    {
        await Sembrar(_ana, 5, leidas: [1, 3]);

        var noLeidas = await Listar(_ana, "?unread=true");
        var leidas = await Listar(_ana, "?unread=false");

        Assert.Equal(3, noLeidas.GetProperty("totalRecords").GetInt64());
        Assert.All(noLeidas.GetProperty("items").EnumerateArray(), i => Assert.False(i.GetProperty("read").GetBoolean()));
        Assert.Equal(2, leidas.GetProperty("totalRecords").GetInt64());
        Assert.All(leidas.GetProperty("items").EnumerateArray(), i => Assert.True(i.GetProperty("read").GetBoolean()));
    }

    [Fact]
    public async Task Listar_SoloDevuelveLasDelUsuario()
    {
        await Sembrar(_ana, 2);
        await Sembrar(_beto, 3);

        Assert.Equal(2, (await Listar(_ana)).GetProperty("totalRecords").GetInt64());
        Assert.Equal(3, (await Listar(_beto)).GetProperty("totalRecords").GetInt64());
    }

    [Fact]
    public async Task Listar_ItemsConLaFormaDelContratoEnEspanolSinDirectorio()
    {
        await Sembrar(_ana, 1);

        var item = (await Listar(_ana)).GetProperty("items")[0];

        var campos = item.EnumerateObject().Select(p => p.Name).ToArray();
        Assert.Equal(["id", "type", "title", "body", "data", "read", "createdAt"], campos);
        Assert.Equal("Tu CV está listo", item.GetProperty("title").GetString());
        Assert.Equal("cv-0", item.GetProperty("data").GetProperty("cvId").GetString());
    }

    [Fact]
    public async Task CambiarIdiomaEnElDirectorio_CambiaLosTextos()
    {
        await Sembrar(_ana, 1);

        var directorio = await _factory.CrearCliente(_ana, "ana@ejemplo.com")
            .PutAsJsonAsync("/notifications/directory/me", new { language = "en" });
        Assert.Equal(HttpStatusCode.OK, directorio.StatusCode);

        var item = (await Listar(_ana)).GetProperty("items")[0];
        Assert.Equal("Your CV is ready", item.GetProperty("title").GetString());
        Assert.Equal("Your CV \"CV 0\" is ready to view.", item.GetProperty("body").GetString());
    }

    [Theory]
    [InlineData("?page=0", "page")]
    [InlineData("?page=-1", "page")]
    [InlineData("?pageSize=0", "pageSize")]
    [InlineData("?pageSize=101", "pageSize")]
    [InlineData("?unread=quizas", "unread")]
    public async Task Listar_ParametrosInvalidos_Responde400(string consulta, string campo)
    {
        var response = await _factory.CrearCliente(_ana).GetAsync($"/notifications{consulta}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await Json(response);
        Assert.Equal(400, body.GetProperty("status").GetInt32());
        Assert.True(body.GetProperty("errors").TryGetProperty(campo, out _));
    }

    [Fact]
    public async Task Listar_PageSizeMaximoDeCien_EsValido()
    {
        var pagina = await Listar(_ana, "?pageSize=100");

        Assert.Equal(100, pagina.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public async Task UnreadCount_CuentaSoloLasNoLeidasDelUsuario()
    {
        await Sembrar(_ana, 4, leidas: [0]);
        await Sembrar(_beto, 2);

        var response = await _factory.CrearCliente(_ana).GetAsync("/notifications/unread-count");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, (await Json(response)).GetProperty("count").GetInt64());
    }

    [Fact]
    public async Task MarcarPropiaComoLeida_Responde200YEsIdempotente()
    {
        var notificacion = (await Sembrar(_ana, 1))[0];
        var cliente = _factory.CrearCliente(_ana);

        var primera = await cliente.PatchAsync($"/notifications/{notificacion.Id}/read", null);
        var segunda = await cliente.PatchAsync($"/notifications/{notificacion.Id}/read", null);

        Assert.Equal(HttpStatusCode.OK, primera.StatusCode);
        var body = await Json(primera);
        Assert.Equal(notificacion.Id, body.GetProperty("id").GetString());
        Assert.True(body.GetProperty("read").GetBoolean());
        var readAt = body.GetProperty("readAt").GetInt64();

        Assert.Equal(HttpStatusCode.OK, segunda.StatusCode);
        Assert.Equal(readAt, (await Json(segunda)).GetProperty("readAt").GetInt64());
    }

    [Fact]
    public async Task MarcarAjenaComoLeida_Responde404SinTocarla()
    {
        var deBeto = (await Sembrar(_beto, 1))[0];

        var response = await _factory.CrearCliente(_ana).PatchAsync($"/notifications/{deBeto.Id}/read", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Notificación no encontrada.", (await Json(response)).GetProperty("message").GetString());
        Assert.Null(deBeto.ReadAt);
    }

    [Theory]
    [InlineData("6711a2f0c3e4b5a6d7e8f901")]
    [InlineData("no-es-un-id")]
    public async Task MarcarInexistente_Responde404(string id)
    {
        var response = await _factory.CrearCliente(_ana).PatchAsync($"/notifications/{id}/read", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ReadAll_MarcaTodasLasDelUsuarioYNoLasDeOtros()
    {
        var deAna = await Sembrar(_ana, 3, leidas: [0]);
        var deBeto = await Sembrar(_beto, 2);
        var readAtOriginal = deAna[0].ReadAt;

        var response = await _factory.CrearCliente(_ana).PostAsync("/notifications/read-all", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.All(deAna, n => Assert.NotNull(n.ReadAt));
        Assert.Equal(readAtOriginal, deAna[0].ReadAt);
        Assert.All(deBeto, n => Assert.Null(n.ReadAt));
    }
}
