using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using Notificaciones.Api.Domain;
using Notificaciones.Api.Services;

namespace Notificaciones.Api.Contracts;

/// <summary>
/// Esquema <c>Notification</c> de openapi.yaml: cada item de GET /notifications y el argumento del mensaje
/// <c>notification</c> de SignalR. <see cref="ReadAt"/> se omite mientras no está leída.
/// </summary>
public record NotificationResponse(
    string Id,
    string Type,
    string Title,
    string Body,
    JsonObject Data,
    bool Read,
    long CreatedAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? ReadAt)
{
    private static readonly JsonWriterSettings RelaxedJson = new() { OutputMode = JsonOutputMode.RelaxedExtendedJson };

    public static NotificationResponse From(Notification notification, RenderedText text) => new(
        notification.Id,
        notification.Type,
        text.Title,
        text.Body,
        JsonNode.Parse(notification.Data.ToJson(RelaxedJson))!.AsObject(),
        notification.ReadAt is not null,
        notification.CreatedAt,
        notification.ReadAt);
}
