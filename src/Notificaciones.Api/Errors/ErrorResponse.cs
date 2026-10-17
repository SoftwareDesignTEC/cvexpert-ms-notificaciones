using System.Text.Json.Serialization;

namespace Notificaciones.Api.Errors;

/// <summary>Formato único de error de la API: <c>{ status, message, errors? }</c>. <c>errors</c> solo se envía en 400.</summary>
public record ErrorResponse(
    int Status,
    string Message,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IDictionary<string, string>? Errors = null);
