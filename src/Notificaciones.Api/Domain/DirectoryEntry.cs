using MongoDB.Bson.Serialization.Attributes;

namespace Notificaciones.Api.Domain;

/// <summary>Correo e idioma de un usuario, en la colección <c>directory</c> (<c>_id</c> = userId).</summary>
public class DirectoryEntry
{
    [BsonId]
    public string UserId { get; set; } = string.Empty;

    /// <summary>Valor de <c>X-User-Email</c> en la última llamada.</summary>
    [BsonElement("email")]
    public string Email { get; set; } = string.Empty;

    /// <summary><c>es</c> o <c>en</c>.</summary>
    [BsonElement("language")]
    public string Language { get; set; } = string.Empty;

    [BsonElement("updatedAt")]
    public long UpdatedAt { get; set; }
}
