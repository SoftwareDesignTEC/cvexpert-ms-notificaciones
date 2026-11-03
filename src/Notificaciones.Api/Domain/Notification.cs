using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Notificaciones.Api.Domain;

/// <summary>Notificación guardada en la colección <c>notifications</c>. Fechas en epoch milisegundos UTC.</summary>
public class Notification
{
    [BsonId, BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    /// <summary>Id del evento de la cola; índice único para descartar duplicados.</summary>
    [BsonElement("eventId")]
    public string EventId { get; set; } = string.Empty;

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("type")]
    public string Type { get; set; } = string.Empty;

    [BsonElement("source")]
    public string Source { get; set; } = string.Empty;

    /// <summary>Campos propios del tipo; solo los que define el contrato.</summary>
    [BsonElement("data")]
    public BsonDocument Data { get; set; } = new();

    [BsonElement("occurredAt")]
    public long OccurredAt { get; set; }

    [BsonElement("createdAt")]
    public long CreatedAt { get; set; }

    /// <summary>Null mientras el usuario no la marque como leída.</summary>
    [BsonElement("readAt")]
    public long? ReadAt { get; set; }
}
