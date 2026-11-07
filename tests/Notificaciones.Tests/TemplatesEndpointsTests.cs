using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using Notificaciones.Api.Domain;
using Notificaciones.Api.Services;
using Notificaciones.Api.Services.Channels;

namespace Notificaciones.Tests;

/// <summary>
/// Las pruebas que guardan plantillas usan su propia API (<see cref="NuevaApi"/>) para no cambiar los textos por defecto
/// que esperan las demás.
/// </summary>
public class TemplatesEndpointsTests : IClassFixture<NotificacionesApiFactory>
{
    private const string Ruta = "/notifications/templates";
    private readonly NotificacionesApiFactory _factory;

    public TemplatesEndpointsTests(NotificacionesApiFactory factory)
    {
        _factory = factory;
    }

    private static NotificacionesApiFactory NuevaApi(Action<IServiceCollection>? servicios = null) => new() { ServiciosExtra = servicios };

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private static HttpRequestMessage Solicitud(string metodo, string ruta)
    {
        var solicitud = new HttpRequestMessage(new HttpMethod(metodo), ruta);
        if (metodo is "PUT") solicitud.Content = JsonContent.Create(new { title = "Título", body = "Cuerpo" });
        return solicitud;
    }

    private static Task<HttpResponseMessage> Guardar(HttpClient cliente, string type, string language, string title, string body) =>
        cliente.PutAsJsonAsync($"{Ruta}/{type}/{language}", new { title, body });

    [Theory]
    [InlineData("GET", Ruta, null)]
    [InlineData("GET", Ruta, "user")]
    [InlineData("GET", Ruta, "Admin")]
    [InlineData("GET", Ruta + "/cv.generated/es", null)]
    [InlineData("PUT", Ruta + "/cv.generated/es", "user")]
    public async Task SinRolAdmin_Responde403ConFormatoDeError(string metodo, string ruta, string? rol)
    {
        var response = await _factory.CrearCliente("usuario-1", "ana@ejemplo.com", rol).SendAsync(Solicitud(metodo, ruta));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await Json(response);
        Assert.Equal(403, body.GetProperty("status").GetInt32());
        Assert.Equal("Se requiere rol de administrador.", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Listar_DevuelveLasOchoPorDefectoEnOrden()
    {
        var response = await _factory.CrearAdmin().GetAsync(Ruta);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = (await Json(response)).EnumerateArray().ToList();
        Assert.Equal(
            [
                "application.status_changed:es", "application.status_changed:en", "cv.generated:es", "cv.generated:en",
                "job.new:es", "job.new:en", "course.recommended:es", "course.recommended:en",
            ],
            items.Select(i => $"{i.GetProperty("type").GetString()}:{i.GetProperty("language").GetString()}"));
        Assert.All(items, i =>
        {
            Assert.Equal(0, i.GetProperty("updatedAt").GetInt64());
            Assert.False(i.TryGetProperty("updatedBy", out _));
        });
    }

    [Fact]
    public async Task Obtener_SinGuardar_DevuelveElTextoPorDefecto()
    {
        var response = await _factory.CrearAdmin().GetAsync($"{Ruta}/application.status_changed/en");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var plantilla = await Json(response);
        Assert.Equal("application.status_changed", plantilla.GetProperty("type").GetString());
        Assert.Equal("en", plantilla.GetProperty("language").GetString());
        Assert.Equal("Your application was updated", plantilla.GetProperty("title").GetString());
        Assert.Equal(
            "Your application for {{jobTitle}} at {{company}} changed to \"{{newStatus}}\".",
            plantilla.GetProperty("body").GetString());
        Assert.Equal(0, plantilla.GetProperty("updatedAt").GetInt64());
        Assert.False(plantilla.TryGetProperty("updatedBy", out _));
    }

    [Theory]
    [InlineData("GET", Ruta + "/cv.deleted/es", "type")]
    [InlineData("GET", Ruta + "/cv.generated/fr", "language")]
    [InlineData("PUT", Ruta + "/job/es", "type")]
    [InlineData("PUT", Ruta + "/job.new/EN", "language")]
    public async Task TipoOIdiomaFueraDelContrato_Responde400(string metodo, string ruta, string campo)
    {
        var response = await _factory.CrearAdmin().SendAsync(Solicitud(metodo, ruta));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await Json(response);
        Assert.Equal(400, body.GetProperty("status").GetInt32());
        Assert.True(body.GetProperty("errors").TryGetProperty(campo, out _));
    }

    [Fact]
    public async Task Guardar_DevuelveLaPlantillaYLaConsultaLaMuestra()
    {
        await using var api = NuevaApi();
        var admin = api.CrearAdmin("lucia@ejemplo.com");
        var antes = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var response = await Guardar(admin, "cv.generated", "es", "CV listo: {{title}}", "Línea 1\nLínea 2 ({{cvId}})");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var guardada = await Json(response);
        Assert.Equal("CV listo: {{title}}", guardada.GetProperty("title").GetString());
        Assert.Equal("Línea 1\nLínea 2 ({{cvId}})", guardada.GetProperty("body").GetString());
        Assert.Equal("lucia@ejemplo.com", guardada.GetProperty("updatedBy").GetString());
        Assert.InRange(guardada.GetProperty("updatedAt").GetInt64(), antes, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        var consultada = await Json(await admin.GetAsync($"{Ruta}/cv.generated/es"));
        Assert.Equal("CV listo: {{title}}", consultada.GetProperty("title").GetString());
        Assert.Equal("lucia@ejemplo.com", consultada.GetProperty("updatedBy").GetString());

        // El otro idioma sigue por defecto.
        var otra = await Json(await admin.GetAsync($"{Ruta}/cv.generated/en"));
        Assert.Equal(0, otra.GetProperty("updatedAt").GetInt64());
    }

    [Fact]
    public async Task Guardar_SinXUserEmail_Responde401()
    {
        await using var api = NuevaApi();

        var response = await Guardar(api.CrearCliente("admin-1", email: null, role: "admin"), "cv.generated", "es", "T", "B");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Falta el encabezado X-User-Email.", (await Json(response)).GetProperty("message").GetString());
        Assert.Empty(await api.Plantillas.ListAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Guardar_ConMarcadoresNoPermitidos_Responde400YNombraLosMarcadores()
    {
        await using var api = NuevaApi();

        var response = await Guardar(api.CrearAdmin(), "job.new", "es", "{{jobTitle}} en {{company}}", "{{title}} {{ location }} {{foo}}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await Json(response)).GetProperty("errors");
        var titulo = errors.GetProperty("title").GetString()!;
        Assert.StartsWith("Marcador no permitido para job.new: {{jobTitle}}.", titulo);
        Assert.Contains("{{matchScore}}", titulo);
        Assert.StartsWith("Marcadores no permitidos para job.new: {{ location }}, {{foo}}.", errors.GetProperty("body").GetString());
        Assert.Empty(await api.Plantillas.ListAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData("", "Cuerpo", "title")]
    [InlineData("Título", "   ", "body")]
    [InlineData(null, "Cuerpo", "title")]
    public async Task Guardar_SinTituloOCuerpo_Responde400(string? title, string body, string campo)
    {
        await using var api = NuevaApi();

        var response = await Guardar(api.CrearAdmin(), "cv.generated", "es", title!, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Es obligatorio y no puede estar vacío.", (await Json(response)).GetProperty("errors").GetProperty(campo).GetString());
    }

    [Fact]
    public async Task Guardar_CambiaLoQueDevuelveElHistorial()
    {
        await using var api = NuevaApi();
        await api.Notificaciones.InsertAsync(Notificacion("ana"), CancellationToken.None);
        var ana = api.CrearCliente("ana");

        // La primera consulta deja el texto por defecto en caché; el PUT debe vaciarla.
        Assert.Equal("Tu CV está listo", await PrimerTitulo(ana));

        await Guardar(api.CrearAdmin(), "cv.generated", "es", "Nuevo CV: {{title}}", "Ya está {{cvId}}");

        Assert.Equal("Nuevo CV: CV para backend", await PrimerTitulo(ana));
    }

    [Fact]
    public async Task Guardar_CambiaLoQueEnviaElDispatcher()
    {
        var canal = new CanalQueCaptura();
        await using var api = NuevaApi(servicios => servicios.AddSingleton<INotificationChannel>(canal));
        var dispatcher = api.Services.GetRequiredService<INotificationDispatcher>();
        await dispatcher.DispatchAsync(Notificacion("ana"), CancellationToken.None);

        await Guardar(api.CrearAdmin(), "cv.generated", "es", "Nuevo CV: {{title}}", "Id {{cvId}}");
        await dispatcher.DispatchAsync(Notificacion("ana"), CancellationToken.None);

        Assert.Equal(["Tu CV está listo", "Nuevo CV: CV para backend"], canal.Entregas.Select(e => e.Text.Title));
        Assert.Equal("Id cv-7", canal.Entregas[1].Text.Body);
    }

    private static async Task<string?> PrimerTitulo(HttpClient cliente)
    {
        var pagina = await Json(await cliente.GetAsync("/notifications"));
        return pagina.GetProperty("items")[0].GetProperty("title").GetString();
    }

    private static Notification Notificacion(string usuario) => new()
    {
        EventId = Guid.NewGuid().ToString(),
        UserId = usuario,
        Type = "cv.generated",
        Source = "cvs",
        Data = new BsonDocument { ["cvId"] = "cv-7", ["title"] = "CV para backend" },
        OccurredAt = 1760720400000,
        CreatedAt = 1760720400000,
    };

    private sealed class CanalQueCaptura : INotificationChannel
    {
        private readonly object _lock = new();
        private readonly List<NotificationDelivery> _entregas = [];

        public IReadOnlyList<NotificationDelivery> Entregas
        {
            get { lock (_lock) return _entregas.ToList(); }
        }

        public string Name => "captura";

        public Task SendAsync(NotificationDelivery delivery, CancellationToken cancellationToken)
        {
            lock (_lock) _entregas.Add(delivery);
            return Task.CompletedTask;
        }
    }
}
