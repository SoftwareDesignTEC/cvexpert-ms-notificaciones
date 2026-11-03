using System.Text.Json.Nodes;
using Notificaciones.Api.Services.Events;

namespace Notificaciones.Tests;

public class NotificationEventValidatorTests
{
    public const string EventId = "3f2c6a1e-8b4d-4c7a-9e21-5d6f7a8b9c0d";

    /// <summary>Evento válido de cada tipo, con los obligatorios y algún opcional.</summary>
    public static JsonObject EventoValido(string type) => type switch
    {
        "application.status_changed" => Sobre(type, "ofertas-postulaciones", new JsonObject
        {
            ["applicationId"] = "1532",
            ["jobOfferId"] = "88",
            ["jobTitle"] = "Desarrollador .NET",
            ["company"] = "Acme",
            ["previousStatus"] = "applied",
            ["newStatus"] = "interview",
        }),
        "cv.generated" => Sobre(type, "cvs", new JsonObject
        {
            ["cvId"] = "cv-7",
            ["title"] = "CV para backend",
            ["templateId"] = null,
        }),
        "job.new" => Sobre(type, "ofertas-postulaciones", new JsonObject
        {
            ["jobOfferId"] = "88",
            ["title"] = "Desarrollador .NET",
            ["company"] = "Acme",
            ["location"] = "San José",
            ["modality"] = "hybrid",
            ["matchScore"] = 87.5,
        }),
        "course.recommended" => Sobre(type, "brecha-cursos", new JsonObject
        {
            ["title"] = "Docker desde cero",
            ["platform"] = "Coursera",
            ["url"] = "https://www.coursera.org/learn/docker",
            ["isPaid"] = false,
            ["duration"] = "12 horas",
        }),
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    private static JsonObject Sobre(string type, string source, JsonObject data) => new()
    {
        ["schemaVersion"] = 1,
        ["eventId"] = EventId,
        ["type"] = type,
        ["userId"] = "usuario-1",
        ["occurredAt"] = 1760716800000,
        ["source"] = source,
        ["data"] = data,
    };

    private static EventValidationResult Validar(JsonObject evento) =>
        NotificationEventValidator.Validate(BinaryData.FromString(evento.ToJsonString()));

    private static void AssertRechazado(EventValidationResult resultado, string fragmento)
    {
        Assert.False(resultado.IsValid);
        Assert.Equal(DeadLetterReasons.SchemaValidationFailed, resultado.Reason);
        Assert.Contains(fragmento, resultado.Description);
    }

    [Theory]
    [InlineData("application.status_changed")]
    [InlineData("cv.generated")]
    [InlineData("job.new")]
    [InlineData("course.recommended")]
    public void EventoValido_DeCadaTipo_EsAceptado(string type)
    {
        var evento = EventoValido(type);

        var resultado = Validar(evento);

        Assert.True(resultado.IsValid, resultado.Description);
        Assert.Equal(EventId, resultado.Event.EventId);
        Assert.Equal(type, resultado.Event.Type);
        Assert.Equal("usuario-1", resultado.Event.UserId);
        Assert.Equal(1760716800000, resultado.Event.OccurredAt);
        Assert.Equal(evento["source"]!.GetValue<string>(), resultado.Event.Source);
        Assert.True(JsonNode.DeepEquals(evento["data"], resultado.Event.Data));
    }

    [Fact]
    public void CamposExtraEnData_SeAceptanPeroNoSeConservan()
    {
        var evento = EventoValido("cv.generated");
        evento["data"]!["campoNuevo"] = "algo";

        var resultado = Validar(evento);

        Assert.True(resultado.IsValid, resultado.Description);
        Assert.False(resultado.Event.Data.ContainsKey("campoNuevo"));
        Assert.Equal("cv-7", resultado.Event.Data["cvId"]!.GetValue<string>());
    }

    [Fact]
    public void OpcionalesOmitidos_SeAceptan()
    {
        var evento = EventoValido("application.status_changed");
        evento["data"]!.AsObject().Remove("previousStatus");

        Assert.True(Validar(evento).IsValid);
    }

    [Theory]
    [InlineData("schemaVersion")]
    [InlineData("eventId")]
    [InlineData("type")]
    [InlineData("userId")]
    [InlineData("occurredAt")]
    [InlineData("source")]
    [InlineData("data")]
    public void CampoDelSobreFaltante_EsRechazado(string campo)
    {
        var evento = EventoValido("job.new");
        evento.Remove(campo);

        AssertRechazado(Validar(evento), $"{campo}: es obligatorio");
    }

    [Theory]
    [InlineData("application.status_changed", "newStatus")]
    [InlineData("application.status_changed", "applicationId")]
    [InlineData("cv.generated", "cvId")]
    [InlineData("job.new", "matchScore")]
    [InlineData("course.recommended", "isPaid")]
    [InlineData("course.recommended", "url")]
    public void CampoObligatorioDeDataFaltante_EsRechazado(string type, string campo)
    {
        var evento = EventoValido(type);
        evento["data"]!.AsObject().Remove(campo);

        AssertRechazado(Validar(evento), $"data.{campo}: es obligatorio");
    }

    [Fact]
    public void TipoDesconocido_EsRechazado()
    {
        var evento = EventoValido("job.new");
        evento["type"] = "user.deleted";

        AssertRechazado(Validar(evento), "type: \"user.deleted\" no es un valor permitido");
    }

    [Fact]
    public void SourceQueNoCorrespondeAlTipo_EsRechazado()
    {
        var evento = EventoValido("cv.generated");
        evento["source"] = "brecha-cursos";

        AssertRechazado(Validar(evento), "source: \"brecha-cursos\" no corresponde al type \"cv.generated\"");
    }

    [Fact]
    public void OccurredAtComoTexto_EsRechazado()
    {
        var evento = EventoValido("job.new");
        evento["occurredAt"] = "2025-10-17T16:00:00Z";

        AssertRechazado(Validar(evento), "occurredAt: debe ser un entero");
    }

    [Fact]
    public void ValorNoPermitido_IndicaElCampoYElValor()
    {
        var evento = EventoValido("application.status_changed");
        evento["data"]!["newStatus"] = "hired";

        var resultado = Validar(evento);

        AssertRechazado(resultado, "data.newStatus: \"hired\" no es un valor permitido");
    }

    [Fact]
    public void IdNumerico_EsRechazado()
    {
        var evento = EventoValido("application.status_changed");
        evento["data"]!["applicationId"] = 1532;

        AssertRechazado(Validar(evento), "data.applicationId: debe ser texto no vacío");
    }

    [Fact]
    public void EventIdQueNoEsUuid_EsRechazado()
    {
        var evento = EventoValido("job.new");
        evento["eventId"] = "evento-1";

        AssertRechazado(Validar(evento), "eventId: debe ser un UUID v4");
    }

    [Fact]
    public void CampoExtraEnElSobre_EsRechazado()
    {
        var evento = EventoValido("job.new");
        evento["language"] = "es";

        AssertRechazado(Validar(evento), "language: campo no permitido fuera de data");
    }

    [Fact]
    public void VariosErrores_SeReportanJuntos()
    {
        var evento = EventoValido("job.new");
        evento.Remove("userId");
        evento["data"]!["modality"] = "presencial";

        var resultado = Validar(evento);

        AssertRechazado(resultado, "userId: es obligatorio");
        Assert.Contains("data.modality: \"presencial\" no es un valor permitido", resultado.Description);
    }

    [Theory]
    [InlineData("{ esto no es json")]
    [InlineData("")]
    public void JsonInvalido_EsInvalidJson(string cuerpo)
    {
        var resultado = NotificationEventValidator.Validate(BinaryData.FromString(cuerpo));

        Assert.False(resultado.IsValid);
        Assert.Equal(DeadLetterReasons.InvalidJson, resultado.Reason);
    }

    [Fact]
    public void CuerpoQueNoEsObjeto_EsRechazado()
    {
        var resultado = NotificationEventValidator.Validate(BinaryData.FromString("[1, 2]"));

        AssertRechazado(resultado, "debe ser un objeto JSON");
    }
}
