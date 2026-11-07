using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Notificaciones.Api.Contracts;

/// <summary>Esquema <c>TemplateUpdate</c>: cuerpo de PUT /notifications/templates/{type}/{language}.</summary>
public class TemplateUpdate
{
    private const string RequiredError = "Es obligatorio y no puede estar vacío.";

    [Required(ErrorMessage = RequiredError)]
    public string? Title { get; set; }

    [Required(ErrorMessage = RequiredError)]
    public string? Body { get; set; }
}

/// <summary>
/// Esquema <c>Template</c>. Una plantilla por defecto (nunca editada) va con <c>updatedAt: 0</c> y sin <c>updatedBy</c>.
/// </summary>
public record TemplateResponse(
    string Type,
    string Language,
    string Title,
    string Body,
    long UpdatedAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? UpdatedBy);
