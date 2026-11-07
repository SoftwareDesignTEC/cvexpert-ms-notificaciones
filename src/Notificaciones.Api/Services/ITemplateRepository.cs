using Notificaciones.Api.Domain;

namespace Notificaciones.Api.Services;

/// <summary>Plantillas guardadas por los administradores. Solo existen las que se editaron alguna vez.</summary>
public interface ITemplateRepository
{
    /// <summary>Todas las plantillas guardadas (a lo sumo 8).</summary>
    Task<IReadOnlyList<NotificationTemplate>> ListAsync(CancellationToken cancellationToken);

    /// <summary>La plantilla guardada de un tipo e idioma, o null si nunca se editó.</summary>
    Task<NotificationTemplate?> GetAsync(string type, string language, CancellationToken cancellationToken);

    /// <summary>Crea o reemplaza la plantilla con el <c>Id</c> indicado.</summary>
    Task UpsertAsync(NotificationTemplate template, CancellationToken cancellationToken);
}
