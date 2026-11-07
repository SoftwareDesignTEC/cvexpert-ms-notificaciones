using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using Notificaciones.Api.Domain;
using Notificaciones.Api.Services;
using Notificaciones.Api.Services.Templates;

namespace Notificaciones.Tests;

public class TemplateNotificationRendererTests
{
    private readonly InMemoryTemplateRepository _plantillas = new();
    private readonly RelojDePrueba _reloj = new();
    private readonly TemplateCache _cache;
    private readonly TemplateNotificationRenderer _renderer;

    public TemplateNotificationRendererTests()
    {
        _cache = new TemplateCache(_plantillas, _reloj);
        _renderer = new TemplateNotificationRenderer(_cache, NullLogger<TemplateNotificationRenderer>.Instance);
    }

    private static Notification Notificacion(string type) => new()
    {
        Type = type,
        Data = type switch
        {
            "application.status_changed" => new BsonDocument
            {
                ["applicationId"] = "1532", ["jobOfferId"] = "88", ["jobTitle"] = "Desarrollador .NET",
                ["company"] = "Acme", ["previousStatus"] = "applied", ["newStatus"] = "interview",
            },
            "cv.generated" => new BsonDocument { ["cvId"] = "cv-7", ["title"] = "CV para backend" },
            "job.new" => new BsonDocument
            {
                ["jobOfferId"] = "88", ["title"] = "Desarrollador .NET", ["company"] = "Acme",
                ["location"] = "San José", ["modality"] = "hybrid", ["matchScore"] = 87.5,
            },
            "course.recommended" => new BsonDocument
            {
                ["title"] = "Docker desde cero", ["platform"] = "Coursera",
                ["url"] = "https://www.coursera.org/learn/docker", ["isPaid"] = false,
            },
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        },
    };

    private Task Guardar(string type, string language, string title, string body) =>
        _plantillas.UpsertAsync(new NotificationTemplate
        {
            Id = NotificationTemplate.IdFor(type, language), Type = type, Language = language,
            Title = title, Body = body, UpdatedAt = 1, UpdatedBy = "admin@ejemplo.com",
        }, CancellationToken.None);

    private ValueTask<RenderedText> Render(Notification notificacion, string idioma) =>
        _renderer.RenderAsync(notificacion, idioma, CancellationToken.None);

    [Theory]
    [InlineData("application.status_changed", "es",
        "Tu postulación cambió de estado", "Tu postulación a Desarrollador .NET en Acme cambió a \"Entrevista\".")]
    [InlineData("application.status_changed", "en",
        "Your application was updated", "Your application for Desarrollador .NET at Acme changed to \"Interview\".")]
    [InlineData("cv.generated", "es", "Tu CV está listo", "Ya puedes ver tu CV \"CV para backend\".")]
    [InlineData("cv.generated", "en", "Your CV is ready", "Your CV \"CV para backend\" is ready to view.")]
    [InlineData("job.new", "es",
        "Nueva oferta para ti", "Desarrollador .NET en Acme · San José · Híbrida. Compatibilidad: 87,5 %.")]
    [InlineData("job.new", "en",
        "A new job for you", "Desarrollador .NET at Acme · San José · Hybrid. Match: 87.5%.")]
    [InlineData("course.recommended", "es", "Curso recomendado", "Docker desde cero en Coursera (gratis).")]
    [InlineData("course.recommended", "en", "Recommended course", "Docker desde cero on Coursera (free).")]
    public async Task SinPlantillaGuardada_UsaElTextoPorDefecto(string type, string idioma, string titulo, string cuerpo)
    {
        var texto = await Render(Notificacion(type), idioma);

        Assert.Equal(titulo, texto.Title);
        Assert.Equal(cuerpo, texto.Body);
    }

    [Fact]
    public async Task ConPlantillaGuardada_LaUsaYTraduceLosValoresDeLista()
    {
        await Guardar("application.status_changed", "es", "Novedades de {{company}}", "De {{previousStatus}} a {{newStatus}}.");

        var texto = await Render(Notificacion("application.status_changed"), "es");

        Assert.Equal("Novedades de Acme", texto.Title);
        Assert.Equal("De Enviada a Entrevista.", texto.Body);
    }

    [Fact]
    public async Task PlantillaGuardadaEnUnIdioma_NoCambiaElOtro()
    {
        await Guardar("job.new", "en", "New: {{title}}", "{{modality}}");

        Assert.Equal("Hybrid", (await Render(Notificacion("job.new"), "en")).Body);
        Assert.Equal("Nueva oferta para ti", (await Render(Notificacion("job.new"), "es")).Title);
    }

    [Fact]
    public async Task IdiomaNoSoportado_UsaEspanol()
    {
        var texto = await Render(Notificacion("cv.generated"), "fr");

        Assert.Equal("Tu CV está listo", texto.Title);
    }

    [Fact]
    public async Task CursoDePago_SeIndicaEnElIdioma()
    {
        var notificacion = Notificacion("course.recommended");
        notificacion.Data["isPaid"] = true;

        Assert.EndsWith("(de pago).", (await Render(notificacion, "es")).Body);
        Assert.EndsWith("(paid).", (await Render(notificacion, "en")).Body);
    }

    [Fact]
    public async Task MarcadorSinValor_QuedaVacio()
    {
        var notificacion = Notificacion("cv.generated");
        notificacion.Data["title"] = BsonNull.Value;

        Assert.Equal("Ya puedes ver tu CV \"\".", (await Render(notificacion, "es")).Body);
    }

    [Fact]
    public async Task Cache_DuraSesentaSegundos()
    {
        await Render(Notificacion("cv.generated"), "es");
        await Guardar("cv.generated", "es", "Cambiado", "Cambiado");

        _reloj.Avanzar(TemplateCache.Duration - TimeSpan.FromSeconds(1));
        Assert.Equal("Tu CV está listo", (await Render(Notificacion("cv.generated"), "es")).Title);
        Assert.Equal(1, _plantillas.Lecturas);

        _reloj.Avanzar(TimeSpan.FromSeconds(1));
        Assert.Equal("Cambiado", (await Render(Notificacion("cv.generated"), "es")).Title);
        Assert.Equal(2, _plantillas.Lecturas);
    }

    [Fact]
    public async Task Invalidar_LeeLasPlantillasDeNuevo()
    {
        await Render(Notificacion("cv.generated"), "es");
        await Guardar("cv.generated", "es", "Cambiado", "Cambiado");

        _cache.Invalidate();

        Assert.Equal("Cambiado", (await Render(Notificacion("cv.generated"), "es")).Title);
    }

    [Fact]
    public async Task PlantillasCaidas_UsaElTextoPorDefecto()
    {
        var renderer = new TemplateNotificationRenderer(
            new TemplateCache(new PlantillasCaidas(), _reloj), NullLogger<TemplateNotificationRenderer>.Instance);

        var texto = await renderer.RenderAsync(Notificacion("cv.generated"), "en", CancellationToken.None);

        Assert.Equal("Your CV is ready", texto.Title);
    }

    private sealed class PlantillasCaidas : ITemplateRepository
    {
        public Task<IReadOnlyList<NotificationTemplate>> ListAsync(CancellationToken cancellationToken) =>
            throw new TimeoutException("Mongo no responde");
        public Task<NotificationTemplate?> GetAsync(string type, string language, CancellationToken cancellationToken) =>
            throw new TimeoutException("Mongo no responde");
        public Task UpsertAsync(NotificationTemplate template, CancellationToken cancellationToken) =>
            throw new TimeoutException("Mongo no responde");
    }
}
