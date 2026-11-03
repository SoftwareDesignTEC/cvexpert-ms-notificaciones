using MongoDB.Bson;
using Notificaciones.Api.Domain;
using Notificaciones.Api.Services;

namespace Notificaciones.Tests;

public class DefaultNotificationRendererTests
{
    private readonly DefaultNotificationRenderer _renderer = new();

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
    public void ArmaTituloYCuerpoPorTipoEIdioma(string type, string idioma, string titulo, string cuerpo)
    {
        var texto = _renderer.Render(Notificacion(type), idioma);

        Assert.Equal(titulo, texto.Title);
        Assert.Equal(cuerpo, texto.Body);
    }

    [Fact]
    public void IdiomaNoSoportado_UsaEspanol()
    {
        var texto = _renderer.Render(Notificacion("cv.generated"), "fr");

        Assert.Equal("Tu CV está listo", texto.Title);
    }

    [Fact]
    public void CursoDePago_SeIndicaEnElIdioma()
    {
        var notificacion = Notificacion("course.recommended");
        notificacion.Data["isPaid"] = true;

        Assert.EndsWith("(de pago).", _renderer.Render(notificacion, "es").Body);
        Assert.EndsWith("(paid).", _renderer.Render(notificacion, "en").Body);
    }

    [Fact]
    public void MarcadorSinValor_QuedaVacio()
    {
        var notificacion = Notificacion("cv.generated");
        notificacion.Data["title"] = BsonNull.Value;

        Assert.Equal("Ya puedes ver tu CV \"\".", _renderer.Render(notificacion, "es").Body);
    }
}
