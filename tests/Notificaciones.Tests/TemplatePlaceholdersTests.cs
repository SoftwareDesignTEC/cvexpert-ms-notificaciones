using System.Text.Json;
using Notificaciones.Api.Services.Templates;

namespace Notificaciones.Tests;

public class TemplatePlaceholdersTests
{
    [Theory]
    [InlineData("application.status_changed", "{{jobTitle}} en {{company}}: {{previousStatus}} → {{newStatus}} ({{applicationId}}, {{jobOfferId}})")]
    [InlineData("cv.generated", "{{title}} {{cvId}} {{templateId}}")]
    [InlineData("job.new", "{{jobOfferId}} {{title}} {{company}} {{location}} {{modality}} {{matchScore}}")]
    [InlineData("course.recommended", "{{title}} {{platform}} {{url}} {{isPaid}} {{duration}} {{skill}}")]
    [InlineData("cv.generated", "Sin marcadores")]
    public void MarcadoresDelDataDelTipo_SePermiten(string type, string texto)
    {
        Assert.Empty(TemplatePlaceholders.FindNotAllowed(type, texto));
    }

    [Theory]
    [InlineData("job.new", "{{jobTitle}}", "{{jobTitle}}")] // existe en otro tipo, no en este
    [InlineData("cv.generated", "{{foo}} y {{foo}}", "{{foo}}")]
    [InlineData("cv.generated", "{{ title }}", "{{ title }}")]
    [InlineData("cv.generated", "{{Title}}", "{{Title}}")]
    [InlineData("cv.generated", "{{job-title}}", "{{job-title}}")]
    [InlineData("cv.generated", "{{}}", "{{}}")]
    public void MarcadorDesconocidoOMalEscrito_SeReporta(string type, string texto, string esperado)
    {
        Assert.Equal([esperado], TemplatePlaceholders.FindNotAllowed(type, texto));
    }

    /// <summary>Los marcadores salen del validador escrito a mano: si el esquema cambia y el validador no, esto falla.</summary>
    [Fact]
    public void MarcadoresPermitidos_CoincidenConElEsquemaDelEvento()
    {
        using var esquema = JsonDocument.Parse(File.ReadAllText(RutaDelEsquema()));
        var raiz = esquema.RootElement;

        foreach (var variante in raiz.GetProperty("oneOf").EnumerateArray())
        {
            var propiedades = variante.GetProperty("properties");
            var type = propiedades.GetProperty("type").GetProperty("const").GetString()!;
            var definicion = propiedades.GetProperty("data").GetProperty("$ref").GetString()!.Split('/')[^1];
            var campos = raiz.GetProperty("$defs").GetProperty(definicion).GetProperty("properties")
                .EnumerateObject().Select(p => p.Name).ToArray();

            Assert.Equal(campos, TemplatePlaceholders.Allowed(type));
        }
        Assert.Equal(NotificationTypes.All.Count, raiz.GetProperty("oneOf").GetArrayLength());
    }

    private static string RutaDelEsquema()
    {
        for (var carpeta = new DirectoryInfo(AppContext.BaseDirectory); carpeta is not null; carpeta = carpeta.Parent)
        {
            var ruta = Path.Combine(carpeta.FullName, "docs", "contratos", "evento-notificacion.schema.json");
            if (File.Exists(ruta)) return ruta;
        }
        throw new FileNotFoundException("No se encontró docs/contratos/evento-notificacion.schema.json");
    }
}
