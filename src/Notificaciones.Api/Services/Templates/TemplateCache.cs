using Notificaciones.Api.Domain;

namespace Notificaciones.Api.Services.Templates;

/// <summary>
/// Plantillas guardadas, en memoria durante <see cref="Duration"/>: se leen todas de una vez (son 8 como máximo).
/// <see cref="Invalidate"/> la vacía al guardar. Es por instancia: con varias instancias del App Service, las otras
/// pueden seguir con el texto anterior hasta que venza su copia.
/// </summary>
public class TemplateCache(ITemplateRepository repository, TimeProvider timeProvider)
{
    public static readonly TimeSpan Duration = TimeSpan.FromSeconds(60);

    private sealed record Snapshot(IReadOnlyDictionary<string, NotificationTemplate> Templates, DateTimeOffset ExpiresAt);

    private readonly object _lock = new();
    private Snapshot? _snapshot;
    private long _version;

    /// <summary>Plantillas guardadas por <c>Id</c> (<c>"{type}:{language}"</c>).</summary>
    public async ValueTask<IReadOnlyDictionary<string, NotificationTemplate>> GetAsync(CancellationToken cancellationToken)
    {
        long version;
        lock (_lock)
        {
            if (_snapshot is { } snapshot && timeProvider.GetUtcNow() < snapshot.ExpiresAt) return snapshot.Templates;
            version = _version;
        }

        var loaded = (await repository.ListAsync(cancellationToken)).ToDictionary(t => t.Id, StringComparer.Ordinal);

        lock (_lock)
        {
            // Si se invalidó mientras se leía, lo leído puede ser anterior al PUT: se devuelve pero no se guarda.
            if (version == _version) _snapshot = new Snapshot(loaded, timeProvider.GetUtcNow() + Duration);
        }
        return loaded;
    }

    public void Invalidate()
    {
        lock (_lock)
        {
            _snapshot = null;
            _version++;
        }
    }
}
