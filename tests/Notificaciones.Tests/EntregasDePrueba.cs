using MongoDB.Bson;
using Notificaciones.Api.Domain;
using Notificaciones.Api.Services;
using Notificaciones.Api.Services.Channels;

namespace Notificaciones.Tests;

/// <summary>Notificaciones y entregas de prueba para los canales.</summary>
public static class EntregasDePrueba
{
    public static Notification Notificacion(string type = "application.status_changed", string userId = "usuario-1") => new()
    {
        Id = "6711a2f0c3e4b5a6d7e8f901",
        EventId = "3f2c6a1e-8b4d-4c7a-9e21-5d6f7a8b9c0d",
        UserId = userId,
        Type = type,
        Source = "ofertas-postulaciones",
        Data = new BsonDocument
        {
            ["applicationId"] = "1532",
            ["jobOfferId"] = "88",
            ["jobTitle"] = "Desarrollador .NET",
            ["company"] = "Acme",
            ["newStatus"] = "interview",
        },
        OccurredAt = 1760716800000,
        CreatedAt = 1760720400000,
        ReadAt = null,
    };

    public static DirectoryEntry Ana(string language = "es", string userId = "usuario-1") =>
        new() { UserId = userId, Email = "ana@ejemplo.com", Language = language };

    /// <summary>Entrega como la arma el compuesto: textos del renderer en el idioma del destinatario (es sin directorio).</summary>
    public static NotificationDelivery Entrega(Notification? notificacion = null, DirectoryEntry? destinatario = null)
    {
        notificacion ??= Notificacion();
        var idioma = destinatario?.Language ?? Languages.Default;
        return new NotificationDelivery(notificacion, destinatario, idioma, new DefaultNotificationRenderer().Render(notificacion, idioma));
    }
}
