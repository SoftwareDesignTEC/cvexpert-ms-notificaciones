using MongoDB.Bson.Serialization.Attributes;

namespace Notificaciones.Api.Domain;

/// <summary>
/// Plantilla editada por un administrador, en la colección <c>templates</c> (<c>_id</c> = <c>"{type}:{language}"</c>).
/// Si un tipo e idioma no tiene documento, se usa el texto por defecto.
/// </summary>
public class NotificationTemplate
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary><c>es</c> o <c>en</c>.</summary>
    [BsonElement("language")]
    public string Language { get; set; } = string.Empty;

    /// <summary>Título con marcadores <c>{{campo}}</c>.</summary>
    [BsonElement("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Cuerpo con marcadores <c>{{campo}}</c>.</summary>
    [BsonElement("body")]
    public string Body { get; set; } = string.Empty;

    [BsonElement("updatedAt")]
    public long UpdatedAt { get; set; }

    /// <summary>Valor de <c>X-User-Email</c> del administrador que la guardó.</summary>
    [BsonElement("updatedBy")]
    public string UpdatedBy { get; set; } = string.Empty;

    public static string IdFor(string type, string language) => $"{type}:{language}";
}
