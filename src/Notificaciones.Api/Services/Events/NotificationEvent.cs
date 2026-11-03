using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;

namespace Notificaciones.Api.Services.Events;

/// <summary>Evento de la cola ya validado. <see cref="Data"/> trae solo los campos que define el contrato para su tipo.</summary>
public record NotificationEvent(
    string EventId,
    string Type,
    string UserId,
    long OccurredAt,
    string Source,
    JsonObject Data);

/// <summary>Valores de <c>DeadLetterReason</c> acordados en el contrato (docs/contratos/README.md, sección 3).</summary>
public static class DeadLetterReasons
{
    public const string InvalidJson = "InvalidJson";
    public const string SchemaValidationFailed = "SchemaValidationFailed";
}

/// <summary>Resultado de validar un mensaje: el evento, o el motivo y el detalle para la dead-letter.</summary>
public record EventValidationResult(NotificationEvent? Event, string? Reason, string? Description)
{
    [MemberNotNullWhen(true, nameof(Event))]
    [MemberNotNullWhen(false, nameof(Reason), nameof(Description))]
    public bool IsValid => Event is not null;

    public static EventValidationResult Valid(NotificationEvent notificationEvent) => new(notificationEvent, null, null);

    public static EventValidationResult Invalid(string reason, string description) => new(null, reason, description);
}
