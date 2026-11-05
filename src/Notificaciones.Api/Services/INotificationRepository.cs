using Notificaciones.Api.Domain;

namespace Notificaciones.Api.Services;

/// <summary>Persistencia de las notificaciones. Todas las consultas se limitan al usuario indicado.</summary>
public interface INotificationRepository
{
    /// <summary>Crea los índices de la colección (eventId único; userId + createdAt descendente; userId + readAt).</summary>
    Task EnsureIndexesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Guarda la notificación. Devuelve <see cref="InsertResult.Duplicate"/> si ya existe una con el mismo eventId;
    /// cualquier otro fallo (base caída, timeout) se propaga como excepción.
    /// </summary>
    Task<InsertResult> InsertAsync(Notification notification, CancellationToken cancellationToken);

    /// <summary>
    /// Página de notificaciones del usuario, más recientes primero. <paramref name="unread"/>: true solo no leídas,
    /// false solo leídas, null todas.
    /// </summary>
    Task<Page<Notification>> ListAsync(string userId, bool? unread, int page, int pageSize, CancellationToken cancellationToken);

    Task<long> CountUnreadAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// Marca como leída si es del usuario; si ya lo estaba, conserva su readAt. Devuelve la notificación,
    /// o null si no existe o es de otro usuario.
    /// </summary>
    Task<Notification?> MarkReadAsync(string id, string userId, long readAt, CancellationToken cancellationToken);

    Task MarkAllReadAsync(string userId, long readAt, CancellationToken cancellationToken);
}

public enum InsertResult
{
    Inserted,
    Duplicate,
}

/// <summary>Una página de resultados y el total sin paginar.</summary>
public record Page<T>(IReadOnlyList<T> Items, long TotalRecords);
