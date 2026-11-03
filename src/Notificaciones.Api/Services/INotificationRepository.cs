using Notificaciones.Api.Domain;

namespace Notificaciones.Api.Services;

/// <summary>Persistencia de las notificaciones.</summary>
public interface INotificationRepository
{
    /// <summary>Crea los índices de la colección (eventId único; userId + createdAt descendente).</summary>
    Task EnsureIndexesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Guarda la notificación. Devuelve <see cref="InsertResult.Duplicate"/> si ya existe una con el mismo eventId;
    /// cualquier otro fallo (base caída, timeout) se propaga como excepción.
    /// </summary>
    Task<InsertResult> InsertAsync(Notification notification, CancellationToken cancellationToken);
}

public enum InsertResult
{
    Inserted,
    Duplicate,
}
