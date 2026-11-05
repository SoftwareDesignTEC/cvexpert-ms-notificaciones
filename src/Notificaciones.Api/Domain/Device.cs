using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Notificaciones.Api.Domain;

/// <summary>Token de FCM de un dispositivo, en la colección <c>devices</c>. Un token pertenece a un solo usuario.</summary>
public class Device
{
    [BsonId, BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    /// <summary>Token de registro de FCM; índice único.</summary>
    [BsonElement("token")]
    public string Token { get; set; } = string.Empty;

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    /// <summary>Por ahora solo <c>android</c>.</summary>
    [BsonElement("platform")]
    public string Platform { get; set; } = string.Empty;

    /// <summary>Cuándo se asoció el token al usuario actual.</summary>
    [BsonElement("registeredAt")]
    public long RegisteredAt { get; set; }
}
