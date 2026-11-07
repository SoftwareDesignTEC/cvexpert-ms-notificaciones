using MongoDB.Driver;
using Notificaciones.Api.Domain;
using Notificaciones.Api.Services;

namespace Notificaciones.Api.Infrastructure.Mongo;

/// <summary>
/// Plantillas en la colección <c>templates</c>. El <c>_id</c> es <c>"{type}:{language}"</c>, así que no necesita más índices;
/// la colección se crea sola con el primer PUT.
/// </summary>
public class MongoTemplateRepository(IMongoDatabase database) : ITemplateRepository
{
    public const string CollectionName = "templates";

    private readonly IMongoCollection<NotificationTemplate> _collection = database.GetCollection<NotificationTemplate>(CollectionName);

    public async Task<IReadOnlyList<NotificationTemplate>> ListAsync(CancellationToken cancellationToken) =>
        await _collection.Find(FilterDefinition<NotificationTemplate>.Empty).ToListAsync(cancellationToken);

    public async Task<NotificationTemplate?> GetAsync(string type, string language, CancellationToken cancellationToken)
    {
        var id = NotificationTemplate.IdFor(type, language);
        return await _collection.Find(t => t.Id == id).FirstOrDefaultAsync(cancellationToken);
    }

    public Task UpsertAsync(NotificationTemplate template, CancellationToken cancellationToken) =>
        _collection.ReplaceOneAsync(
            t => t.Id == template.Id, template, new ReplaceOptions { IsUpsert = true }, cancellationToken);
}
